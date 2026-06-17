using System.Text.Json;
using EtoroPortfolioHub.Models;
using Microsoft.Extensions.Options;

namespace EtoroPortfolioHub.Services;

public sealed class EtoroRestClient
{
    private readonly HttpClient _httpClient;
    private readonly EtoroOptions _options;
    private readonly ILogger<EtoroRestClient> _logger;

    public EtoroRestClient(
        HttpClient httpClient,
        IOptions<EtoroOptions> options,
        ILogger<EtoroRestClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PortfolioSnapshot> GetPortfolioSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var url = BuildPortfolioUrl();

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("x-user-key", _options.UserKey);
        request.Headers.Add("x-request-id", Guid.NewGuid().ToString());

        _logger.LogInformation("Chiamata eToro verso URL: {Url}", url);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Errore chiamata eToro. Status: {StatusCode}. URL: {Url}. Body: {Body}",
                response.StatusCode,
                url,
                raw);

            response.EnsureSuccessStatusCode();
        }

        var snapshot = ParsePortfolioResponse(raw);

        var instrumentIds = snapshot.Positions
            .Select(p => p.InstrumentId)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        // 1) Enrichment con nome, simbolo e tipo ufficiale eToro
        var metadata = await GetInstrumentMetadataAsync(instrumentIds, cancellationToken);

        foreach (var position in snapshot.Positions)
        {
            if (metadata.TryGetValue(position.InstrumentId, out var info))
            {
                if (string.IsNullOrWhiteSpace(position.InstrumentName))
                    position.InstrumentName = info.InstrumentDisplayName;

                if (string.IsNullOrWhiteSpace(position.Symbol))
                    position.Symbol = info.SymbolFull;

                position.InstrumentTypeId = info.InstrumentTypeId;
                position.InstrumentTypeDescription = info.InstrumentTypeDescription;
            }

            if (string.IsNullOrWhiteSpace(position.InstrumentName))
                position.InstrumentName = $"Strumento {position.InstrumentId}";

            if (string.IsNullOrWhiteSpace(position.Symbol))
                position.Symbol = position.InstrumentId.ToString();
        }

        // 2) Enrichment con prezzi correnti
        var rates = await GetInstrumentRatesAsync(instrumentIds, cancellationToken);

        foreach (var position in snapshot.Positions)
        {
            if (rates.TryGetValue(position.InstrumentId, out var rate))
            {
                position.Bid = rate.Bid;
                position.Ask = rate.Ask;
                position.LastExecution = rate.LastExecution;
                position.ConversionRateBid = rate.ConversionRateBid;
                position.ConversionRateAsk = rate.ConversionRateAsk;

                position.CurrentRate = rate.LastExecution != 0m
                    ? rate.LastExecution
                    : (rate.Bid != 0m ? rate.Bid : rate.Ask);

                if (position.Timestamp is null)
                    position.Timestamp = rate.Date;
            }
        }

        return snapshot;
    }

    private string BuildPortfolioUrl()
    {
        const string root = "https://public-api.etoro.com";

        return _options.Environment.Equals("Demo", StringComparison.OrdinalIgnoreCase)
            ? $"{root}/api/v1/trading/info/demo/pnl"
            : $"{root}/api/v1/trading/info/real/pnl";
    }

    private async Task<Dictionary<int, string>> GetInstrumentTypeDescriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var url = "https://public-api.etoro.com/api/v1/market-data/instrument-types";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("x-user-key", _options.UserKey);
        request.Headers.Add("x-request-id", Guid.NewGuid().ToString());

        _logger.LogInformation("Chiamata instrument types verso URL: {Url}", url);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Errore instrument types. Status: {StatusCode}. URL: {Url}. Body: {Body}",
                response.StatusCode,
                url,
                raw);

            return new Dictionary<int, string>();
        }

        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;

        var result = new Dictionary<int, string>();

        if (root.TryGetProperty("instrumentTypes", out var items) &&
            items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                var id = GetInt32(item, "instrumentTypeID", "instrumentTypeId");
                var description = GetString(item, "instrumentTypeDescription");

                if (id > 0 && !string.IsNullOrWhiteSpace(description))
                {
                    result[id] = description;
                }
            }
        }

        return result;
    }

    private async Task<Dictionary<int, InstrumentMetadataDto>> GetInstrumentMetadataAsync(
        IEnumerable<int> instrumentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = instrumentIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<int, InstrumentMetadataDto>();

        var typeDescriptions = await GetInstrumentTypeDescriptionsAsync(cancellationToken);

        var url =
            $"https://public-api.etoro.com/api/v1/market-data/instruments?instrumentIds={string.Join(",", ids)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("x-user-key", _options.UserKey);
        request.Headers.Add("x-request-id", Guid.NewGuid().ToString());

        _logger.LogInformation("Chiamata metadata strumenti verso URL: {Url}", url);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Errore metadata strumenti. Status: {StatusCode}. URL: {Url}. Body: {Body}",
                response.StatusCode,
                url,
                raw);

            return new Dictionary<int, InstrumentMetadataDto>();
        }

        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;

        var result = new Dictionary<int, InstrumentMetadataDto>();

        if (root.TryGetProperty("instrumentDisplayDatas", out var items) &&
            items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                var instrumentId = GetInt32(item, "instrumentID", "instrumentId");
                if (instrumentId == 0)
                    continue;

                var instrumentTypeId = GetInt32(item, "instrumentTypeID", "instrumentTypeId");

                result[instrumentId] = new InstrumentMetadataDto
                {
                    InstrumentId = instrumentId,
                    InstrumentDisplayName = GetString(item, "instrumentDisplayName"),
                    SymbolFull = GetString(item, "symbolFull"),
                    InstrumentTypeId = instrumentTypeId,
                    InstrumentTypeDescription = typeDescriptions.TryGetValue(instrumentTypeId, out var description)
                        ? description
                        : string.Empty
                };
            }
        }

        return result;
    }

    private async Task<Dictionary<int, InstrumentRateDto>> GetInstrumentRatesAsync(
        IEnumerable<int> instrumentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = instrumentIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return new Dictionary<int, InstrumentRateDto>();

        var url =
            $"https://public-api.etoro.com/api/v1/market-data/instruments/rates?instrumentIds={string.Join(",", ids)}";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", _options.ApiKey);
        request.Headers.Add("x-user-key", _options.UserKey);
        request.Headers.Add("x-request-id", Guid.NewGuid().ToString());

        _logger.LogInformation("Chiamata market-data rates verso URL: {Url}", url);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Errore market-data rates. Status: {StatusCode}. URL: {Url}. Body: {Body}",
                response.StatusCode,
                url,
                raw);

            return new Dictionary<int, InstrumentRateDto>();
        }

        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;

        var result = new Dictionary<int, InstrumentRateDto>();

        if (root.TryGetProperty("rates", out var rates) &&
            rates.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in rates.EnumerateArray())
            {
                var instrumentId = GetInt32(item, "instrumentID", "instrumentId");
                if (instrumentId == 0)
                    continue;

                result[instrumentId] = new InstrumentRateDto
                {
                    InstrumentId = instrumentId,
                    Ask = GetDecimal(item, "ask"),
                    Bid = GetDecimal(item, "bid"),
                    LastExecution = GetDecimal(item, "lastExecution"),
                    ConversionRateAsk = GetDecimal(item, "conversionRateAsk"),
                    ConversionRateBid = GetDecimal(item, "conversionRateBid"),
                    Date = GetDateTimeOffset(item, "date")
                };
            }
        }

        return result;
    }

    private PortfolioSnapshot ParsePortfolioResponse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        var snapshot = new PortfolioSnapshot
        {
            LastUpdated = DateTimeOffset.UtcNow
        };

        JsonElement portfolioElement = default;
        var hasClientPortfolio = false;

        if (root.TryGetProperty("clientPortfolio", out var clientPortfolio) &&
            clientPortfolio.ValueKind == JsonValueKind.Object)
        {
            portfolioElement = clientPortfolio;
            hasClientPortfolio = true;
        }

        if (!hasClientPortfolio && root.ValueKind == JsonValueKind.Object)
        {
            portfolioElement = root;
            hasClientPortfolio = true;
        }

        if (!hasClientPortfolio)
            return snapshot;

        snapshot.Credit = GetDecimal(portfolioElement, "credit");
        snapshot.UnrealizedPnL = GetDecimal(portfolioElement, "unrealizedPnL");

        var rootAvailableBalance = GetDecimal(root, "availableBalance");
        var rootTotalBalance = GetDecimal(root, "totalBalance");
        var rootEquity = GetDecimal(root, "equity");
        var rootUnrealizedPnL = GetDecimal(root, "unrealizedPnL");

        if (snapshot.UnrealizedPnL == 0m && rootUnrealizedPnL != 0m)
            snapshot.UnrealizedPnL = rootUnrealizedPnL;

        var directPositionProfitLoss = 0m;
        JsonElement positionsElement = default;
        var foundPositions = false;

        if (hasClientPortfolio &&
            portfolioElement.TryGetProperty("positions", out var clientPositions) &&
            clientPositions.ValueKind == JsonValueKind.Array)
        {
            positionsElement = clientPositions;
            foundPositions = true;
        }
        else if (root.TryGetProperty("positions", out var rootPositions) &&
                 rootPositions.ValueKind == JsonValueKind.Array)
        {
            positionsElement = rootPositions;
            foundPositions = true;
        }

        if (foundPositions)
        {
            foreach (var item in positionsElement.EnumerateArray())
            {
                var positionProfit = GetNestedDecimal(item, "unrealizedPnL", "pnL");
                if (positionProfit == 0m)
                    positionProfit = GetDecimal(item, "pnL", "netProfit");

                var position = new PositionDto
                {
                    PositionId = GetInt64(item, "positionID", "positionId"),
                    InstrumentId = GetInt32(item, "instrumentID", "instrumentId"),
                    Symbol = GetString(item, "symbol"),
                    InstrumentName = GetString(item, "instrumentName"),
                    IsBuy = GetBool(item, "isBuy"),

                    InvestedAmount = GetDecimal(item, "amount", "investedAmount", "initialAmountInDollars"),
                    OpenRate = GetDecimal(item, "openRate"),
                    CurrentRate = GetDecimal(item, "currentRate", "closeRate"),
                    NetProfit = positionProfit,

                    Units = GetDecimal(item, "units"),
                    Leverage = GetDecimal(item, "leverage"),
                    TakeProfitRate = GetDecimal(item, "takeProfitRate"),
                    StopLossRate = GetDecimal(item, "stopLossRate"),

                    OpenConversionRate = GetDecimal(item, "openConversionRate"),

                    Timestamp = GetDateTimeOffset(item, "timestamp", "openDateTime")
                };

                snapshot.Positions.Add(position);
                directPositionProfitLoss += positionProfit;
            }
        }

        if (rootAvailableBalance != 0m)
        {
            snapshot.AvailableCash = rootAvailableBalance;
        }
        else
        {
            var pendingOrdersAmount = 0m;

            if (hasClientPortfolio &&
                portfolioElement.TryGetProperty("ordersForOpen", out var ordersForOpen) &&
                ordersForOpen.ValueKind == JsonValueKind.Array)
            {
                foreach (var order in ordersForOpen.EnumerateArray())
                {
                    var mirrorId = GetInt64(order, "mirrorID", "mirrorId");
                    var amount = GetDecimal(order, "amount");

                    if (mirrorId == 0)
                        pendingOrdersAmount += amount;
                }
            }

            if (hasClientPortfolio &&
                portfolioElement.TryGetProperty("orders", out var orders) &&
                orders.ValueKind == JsonValueKind.Array)
            {
                foreach (var order in orders.EnumerateArray())
                {
                    pendingOrdersAmount += GetDecimal(order, "amount");
                }
            }

            snapshot.AvailableCash = snapshot.Credit - pendingOrdersAmount;
        }

        if (snapshot.UnrealizedPnL != 0m)
        {
            snapshot.ProfitLoss = snapshot.UnrealizedPnL;
        }
        else if (rootEquity != 0m || rootTotalBalance != 0m)
        {
            snapshot.ProfitLoss = rootEquity - rootTotalBalance;
            snapshot.UnrealizedPnL = snapshot.ProfitLoss;
        }
        else
        {
            var mirrorProfitLoss = 0m;

            if (hasClientPortfolio &&
                portfolioElement.TryGetProperty("mirrors", out var mirrors) &&
                mirrors.ValueKind == JsonValueKind.Array)
            {
                foreach (var mirror in mirrors.EnumerateArray())
                {
                    mirrorProfitLoss += GetDecimal(mirror, "closedPositionsNetProfit");

                    if (mirror.TryGetProperty("positions", out var mirrorPositions) &&
                        mirrorPositions.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var mirrorPosition in mirrorPositions.EnumerateArray())
                        {
                            var mirrorPositionProfit = GetNestedDecimal(mirrorPosition, "unrealizedPnL", "pnL");
                            if (mirrorPositionProfit == 0m)
                                mirrorPositionProfit = GetDecimal(mirrorPosition, "pnL", "netProfit");

                            mirrorProfitLoss += mirrorPositionProfit;
                        }
                    }
                }
            }

            snapshot.ProfitLoss = directPositionProfitLoss + mirrorProfitLoss;

            if (snapshot.UnrealizedPnL == 0m && snapshot.ProfitLoss != 0m)
                snapshot.UnrealizedPnL = snapshot.ProfitLoss;
        }

        return snapshot;
    }

    private static string GetString(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                return prop.ValueKind == JsonValueKind.String
                    ? prop.GetString() ?? string.Empty
                    : prop.ToString();
            }
        }

        return string.Empty;
    }

    private static bool GetBool(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.True) return true;
                if (prop.ValueKind == JsonValueKind.False) return false;

                if (prop.ValueKind == JsonValueKind.String &&
                    bool.TryParse(prop.GetString(), out var value))
                {
                    return value;
                }
            }
        }

        return false;
    }

    private static int GetInt32(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var value))
                    return value;

                if (prop.ValueKind == JsonValueKind.String &&
                    int.TryParse(prop.GetString(), out value))
                {
                    return value;
                }
            }
        }

        return 0;
    }

    private static long GetInt64(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out var value))
                    return value;

                if (prop.ValueKind == JsonValueKind.String &&
                    long.TryParse(prop.GetString(), out value))
                {
                    return value;
                }
            }
        }

        return 0;
    }

    private static decimal GetDecimal(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out var value))
                    return value;

                if (prop.ValueKind == JsonValueKind.String &&
                    decimal.TryParse(
                        prop.GetString(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out value))
                {
                    return value;
                }
            }
        }

        return 0m;
    }

    private static decimal GetNestedDecimal(JsonElement element, params string[] path)
    {
        var current = element;

        foreach (var segment in path)
        {
            if (!current.TryGetProperty(segment, out current))
                return 0m;
        }

        if (current.ValueKind == JsonValueKind.Number && current.TryGetDecimal(out var number))
            return number;

        if (current.ValueKind == JsonValueKind.String &&
            decimal.TryParse(
                current.GetString(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsed))
        {
            return parsed;
        }

        return 0m;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (element.TryGetProperty(name, out var prop))
            {
                if (prop.ValueKind == JsonValueKind.String &&
                    DateTimeOffset.TryParse(prop.GetString(), out var value))
                {
                    return value;
                }

                if (prop.ValueKind == JsonValueKind.Number &&
                    prop.TryGetInt64(out var unixValue))
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(unixValue);
                }
            }
        }

        return null;
    }
}
