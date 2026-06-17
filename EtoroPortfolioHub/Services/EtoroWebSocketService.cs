using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using EtoroPortfolioHub.Models;
using EtoroPortfolioHub.State;
using Microsoft.Extensions.Options;

namespace EtoroPortfolioHub.Services;

public sealed class EtoroWebSocketService : BackgroundService
{
    private readonly ILogger<EtoroWebSocketService> _logger;
    private readonly PortfolioState _portfolioState;
    private readonly EtoroOptions _options;

    private readonly HashSet<int> _subscribedInstrumentIds = new();

    public EtoroWebSocketService(
        ILogger<EtoroWebSocketService> logger,
        PortfolioState portfolioState,
        IOptions<EtoroOptions> options)
    {
        _logger = logger;
        _portfolioState = portfolioState;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();

            try
            {
                var uri = new Uri(_options.WebSocketUrl);
                _logger.LogInformation("Connessione WebSocket eToro verso {Uri}", uri);

                await ws.ConnectAsync(uri, stoppingToken);

                await AuthenticateAsync(ws, stoppingToken);
                await SyncSubscriptionsAsync(ws, stoppingToken);

                // Topic privato opzionale per eventi ordini/portfolio
                await SubscribePrivateAsync(ws, stoppingToken);

                var monitorTask = MonitorSubscriptionsAsync(ws, stoppingToken);
                var receiveTask = ReceiveLoopAsync(ws, stoppingToken);

                await Task.WhenAny(monitorTask, receiveTask);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore WebSocket eToro. Nuovo tentativo tra 5 secondi...");
            }

            _subscribedInstrumentIds.Clear();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task AuthenticateAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var authRequest = new
        {
            id = Guid.NewGuid().ToString(),
            operation = "Authenticate",
            data = new
            {
                userKey = _options.UserKey,
                apiKey = _options.ApiKey
            }
        };

        await SendJsonAsync(ws, authRequest, ct);
        _logger.LogInformation("Messaggio Authenticate inviato al WebSocket eToro.");
    }

    private async Task SubscribePrivateAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var request = new
        {
            id = Guid.NewGuid().ToString(),
            operation = "Subscribe",
            data = new
            {
                topics = new[] { "private" },
                snapshot = false
            }
        };

        await SendJsonAsync(ws, request, ct);
        _logger.LogInformation("Sottoscrizione topic private inviata.");
    }

    private async Task MonitorSubscriptionsAsync(ClientWebSocket ws, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            await SyncSubscriptionsAsync(ws, ct);
            await Task.Delay(TimeSpan.FromSeconds(15), ct);
        }
    }

    private async Task SyncSubscriptionsAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var desired = _portfolioState.GetInstrumentIds().ToHashSet();

        var toSubscribe = desired.Except(_subscribedInstrumentIds).ToList();
        var toUnsubscribe = _subscribedInstrumentIds.Except(desired).ToList();

        if (toSubscribe.Count > 0)
        {
            var topics = toSubscribe.Select(id => $"instrument:{id}").ToArray();

            var subscribeRequest = new
            {
                id = Guid.NewGuid().ToString(),
                operation = "Subscribe",
                data = new
                {
                    topics,
                    snapshot = true
                }
            };

            await SendJsonAsync(ws, subscribeRequest, ct);

            foreach (var id in toSubscribe)
                _subscribedInstrumentIds.Add(id);

            _logger.LogInformation("Sottoscritti {Count} strumenti live.", toSubscribe.Count);
        }

        if (toUnsubscribe.Count > 0)
        {
            var topics = toUnsubscribe.Select(id => $"instrument:{id}").ToArray();

            var unsubscribeRequest = new
            {
                id = Guid.NewGuid().ToString(),
                operation = "Unsubscribe",
                data = new
                {
                    topics
                }
            };

            await SendJsonAsync(ws, unsubscribeRequest, ct);

            foreach (var id in toUnsubscribe)
                _subscribedInstrumentIds.Remove(id);

            _logger.LogInformation("Annullata sottoscrizione di {Count} strumenti live.", toUnsubscribe.Count);
        }
    }

    private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];

        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();

            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(buffer, ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogWarning("WebSocket eToro chiuso dal server.");
                    return;
                }

                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            var json = Encoding.UTF8.GetString(ms.ToArray());
            await HandleMessageAsync(json, ct);
        }
    }

    private async Task HandleMessageAsync(string json, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Messaggi standard del WebSocket eToro
            if (root.TryGetProperty("messages", out var messages) &&
                messages.ValueKind == JsonValueKind.Array)
            {
                foreach (var message in messages.EnumerateArray())
                {
                    var topic = message.TryGetProperty("topic", out var topicEl)
                        ? topicEl.GetString()
                        : null;

                    var contentRaw = message.TryGetProperty("content", out var contentEl)
                        ? contentEl.GetString()
                        : null;

                    if (string.IsNullOrWhiteSpace(topic) || string.IsNullOrWhiteSpace(contentRaw))
                        continue;

                    // Quote live strumento
                    if (topic.StartsWith("instrument:", StringComparison.OrdinalIgnoreCase))
                    {
                        var instrumentText = topic.Split(':', 2)[1];
                        if (!int.TryParse(instrumentText, out var instrumentId))
                            continue;

                        using var contentDoc = JsonDocument.Parse(contentRaw);
                        var content = contentDoc.RootElement;

                        var update = new LiveRateUpdateDto
                        {
                            InstrumentId = instrumentId,
                            Ask = GetDecimal(content, "Ask"),
                            Bid = GetDecimal(content, "Bid"),
                            LastExecution = GetDecimal(content, "LastExecution"),
                            ConversionRateAsk = GetDecimal(content, "ConversionRateAsk"),
                            ConversionRateBid = GetDecimal(content, "ConversionRateBid"),
                            Date = GetDateTimeOffset(content, "Date")
                        };

                        _portfolioState.ApplyLiveRate(update);
                    }
                    else if (string.Equals(topic, "private", StringComparison.OrdinalIgnoreCase))
                    {
                        // Qui puoi intercettare in futuro eventi ordini/portfolio
                        _logger.LogDebug("Evento private ricevuto dal WebSocket eToro.");
                    }
                }
            }
            else if (root.TryGetProperty("success", out _))
            {
                // Ack di Authenticate / Subscribe / Unsubscribe
                _logger.LogDebug("Ack WebSocket: {Json}", json);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Messaggio WebSocket non parsabile: {Json}", json);
        }

        await Task.CompletedTask;
    }

    private static async Task SendJsonAsync(ClientWebSocket ws, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private static decimal GetDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return 0m;

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

        return 0m;
    }

    private static DateTimeOffset? GetDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            return null;

        if (prop.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(prop.GetString(), out var value))
        {
            return value;
        }

        return null;
    }
}