using System.Net.Http.Json;
using System.Text.Json;
using EtoroPortfolioHub.Data;
using EtoroPortfolioHub.Data.Entities;
using EtoroPortfolioHub.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace EtoroPortfolioHub.Services;

public sealed class EtoroCredentialService
{
    private const string ProtectorPurpose = "EtoroPortfolioHub.EtoroUserKey.v1";

    private readonly ApplicationDbContext _db;
    private readonly CurrentUserService _currentUserService;
    private readonly IDataProtector _protector;
    private readonly HttpClient _httpClient;
    private readonly EtoroOptions _options;

    public EtoroCredentialService(
        ApplicationDbContext db,
        CurrentUserService currentUserService,
        IDataProtectionProvider dataProtectionProvider,
        HttpClient httpClient,
        IOptions<EtoroOptions> options)
    {
        _db = db;
        _currentUserService = currentUserService;
        _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<EtoroConnectionItem> GetCurrentAsync()
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var entity = await _db.EtoroConnections
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (entity is null)
        {
            return new EtoroConnectionItem
            {
                IsConfigured = false,
                Environment = "Demo",
                PermissionMode = "Read"
            };
        }

        return new EtoroConnectionItem
        {
            IsConfigured = !string.IsNullOrWhiteSpace(entity.EncryptedUserKey),
            Environment = entity.Environment,
            PermissionMode = entity.PermissionMode,
            Gcid = entity.Gcid,
            RealCid = entity.RealCid,
            DemoCid = entity.DemoCid,
            LastSuccessfulValidationUtc = entity.LastSuccessfulValidationUtc,
            LastValidationMessage = entity.LastValidationMessage
        };
    }

    public async Task SaveAsync(
        string userKey,
        string environment,
        string permissionMode)
    {
        if (string.IsNullOrWhiteSpace(userKey))
            throw new InvalidOperationException("La User Key eToro è obbligatoria.");

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("La Public API Key eToro dell'applicazione non è configurata.");

        environment = NormalizeEnvironment(environment);
        permissionMode = NormalizePermissionMode(permissionMode);

        var userId = await _currentUserService.GetRequiredUserIdAsync();
        var utcNow = DateTime.UtcNow;

        var encryptedUserKey = _protector.Protect(userKey.Trim());

        var entity = await _db.EtoroConnections
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (entity is null)
        {
            entity = new EtoroConnectionEntity
            {
                UserId = userId,
                Environment = environment,
                PermissionMode = permissionMode,
                EncryptedUserKey = encryptedUserKey,
                CreatedUtc = utcNow,
                UpdatedUtc = utcNow
            };

            await _db.EtoroConnections.AddAsync(entity);
        }
        else
        {
            entity.Environment = environment;
            entity.PermissionMode = permissionMode;
            entity.EncryptedUserKey = encryptedUserKey;
            entity.UpdatedUtc = utcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<EtoroConnectionTestResult> TestCurrentConnectionAsync()
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var entity = await _db.EtoroConnections
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (entity is null || string.IsNullOrWhiteSpace(entity.EncryptedUserKey))
        {
            return new EtoroConnectionTestResult
            {
                Success = false,
                Message = "Nessuna User Key eToro configurata."
            };
        }

        var userKey = _protector.Unprotect(entity.EncryptedUserKey);

        return await TestAndPersistResultAsync(entity, userKey);
    }

    public async Task<EtoroConnectionTestResult> TestTemporaryConnectionAsync(
        string userKey,
        string environment,
        string permissionMode)
    {
        if (string.IsNullOrWhiteSpace(userKey))
        {
            return new EtoroConnectionTestResult
            {
                Success = false,
                Message = "Inserisci una User Key eToro."
            };
        }

        environment = NormalizeEnvironment(environment);
        permissionMode = NormalizePermissionMode(permissionMode);

        var result = await CallMeEndpointAsync(userKey);

        return result;
    }

    public async Task<string> GetRequiredUserKeyAsync()
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var entity = await _db.EtoroConnections
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (entity is null || string.IsNullOrWhiteSpace(entity.EncryptedUserKey))
            throw new InvalidOperationException("Account eToro non collegato.");

        return _protector.Unprotect(entity.EncryptedUserKey);
    }

    public async Task<string> GetRequiredEnvironmentAsync()
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var entity = await _db.EtoroConnections
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (entity is null)
            throw new InvalidOperationException("Account eToro non collegato.");

        return entity.Environment;
    }

    public async Task DeleteAsync()
    {
        var userId = await _currentUserService.GetRequiredUserIdAsync();

        var entity = await _db.EtoroConnections
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (entity is null)
            return;

        _db.EtoroConnections.Remove(entity);
        await _db.SaveChangesAsync();
    }

    private async Task<EtoroConnectionTestResult> TestAndPersistResultAsync(
        EtoroConnectionEntity entity,
        string userKey)
    {
        var result = await CallMeEndpointAsync(userKey);

        entity.LastValidationMessage = result.Message;
        entity.UpdatedUtc = DateTime.UtcNow;

        if (result.Success)
        {
            entity.Gcid = result.Gcid;
            entity.RealCid = result.RealCid;
            entity.DemoCid = result.DemoCid;
            entity.LastSuccessfulValidationUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return result;
    }

    private async Task<EtoroConnectionTestResult> CallMeEndpointAsync(string userKey)
    {
        try
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://public-api.etoro.com/api/v1/me");

            request.Headers.Add("x-api-key", _options.ApiKey);
            request.Headers.Add("x-user-key", userKey);
            request.Headers.Add("x-request-id", Guid.NewGuid().ToString());

            using var response = await _httpClient.SendAsync(request);
            var raw = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                return new EtoroConnectionTestResult
                {
                    Success = false,
                    Message = $"Connessione non valida. Status: {(int)response.StatusCode} {response.StatusCode}. Body: {raw}"
                };
            }

            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;

            return new EtoroConnectionTestResult
            {
                Success = true,
                Message = "Connessione eToro verificata correttamente.",
                Gcid = TryGetInt64(root, "gcid"),
                RealCid = TryGetInt64(root, "realCid"),
                DemoCid = TryGetInt64(root, "demoCid")
            };
        }
        catch (Exception ex)
        {
            return new EtoroConnectionTestResult
            {
                Success = false,
                Message = $"Errore durante il test connessione: {ex.Message}"
            };
        }
    }

    private static string NormalizeEnvironment(string environment)
    {
        if (string.Equals(environment, "Real", StringComparison.OrdinalIgnoreCase))
            return "Real";

        return "Demo";
    }

    private static string NormalizePermissionMode(string permissionMode)
    {
        if (string.Equals(permissionMode, "Write", StringComparison.OrdinalIgnoreCase))
            return "Write";

        return "Read";
    }

    private static long? TryGetInt64(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var prop))
            return null;

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var value))
            return value;

        if (prop.ValueKind == JsonValueKind.String && long.TryParse(prop.GetString(), out value))
            return value;

        return null;
    }
}

public sealed class EtoroConnectionTestResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;

    public long? Gcid { get; set; }
    public long? RealCid { get; set; }
    public long? DemoCid { get; set; }
}