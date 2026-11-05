using System.Net.WebSockets;
using System.Text.Json;
using Jajcarz.Application.Panels;

namespace Jajcarz.Server.Transport;

/// <summary>Obsługa wiadomości jednego połączenia. Każda wiadomość dostaje własny zakres usług.</summary>
public sealed class PanelSession(string deviceId, PanelConnection connection, IServiceScopeFactory scopes, ILogger logger)
{
    private bool _introduced;

    /// <summary>Zwraca status zamknięcia połączenia.</summary>
    public async Task<WebSocketCloseStatus> RunAsync(CancellationToken cancellationToken)
    {
        while (await connection.ReceiveAsync(cancellationToken) is { } text)
        {
            if (!await HandleAsync(text, cancellationToken))
            {
                return WebSocketCloseStatus.PolicyViolation;
            }
        }
        return WebSocketCloseStatus.NormalClosure;
    }

    public async Task EndAsync()
    {
        if (_introduced)
        {
            await WithPanelAsync(panel => panel.DisconnectAsync(deviceId, connection, CancellationToken.None));
        }
    }

    private async Task<bool> HandleAsync(string text, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            var type = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("t", out var t) ? t.GetString() : null;
            switch (type)
            {
                case "hello":
                    return await OnHelloAsync(root, cancellationToken);
                case "state_change" when _introduced:
                    var change = root.Deserialize<PanelStateChange>(PanelJson.Options)!;
                    await WithPanelAsync(panel => panel.ApplyStateChangeAsync(deviceId, change, cancellationToken));
                    break;
                case "unassigned_press" when _introduced:
                    var press = root.Deserialize<PanelUnassignedPress>(PanelJson.Options)!;
                    await WithPanelAsync(panel =>
                    {
                        panel.ReportUnassignedPress(deviceId, press);
                        return Task.CompletedTask;
                    });
                    break;
                default:
                    logger.LogWarning("Panel {DeviceId}: nieobsługiwana wiadomość {Text}", deviceId, text);
                    break;
            }
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Panel {DeviceId}: błędna wiadomość {Text}", deviceId, text);
        }
        return true;
    }

    private async Task<bool> OnHelloAsync(JsonElement root, CancellationToken cancellationToken)
    {
        var hello = root.Deserialize<PanelHello>(PanelJson.Options)!;
        if (hello.DeviceId != deviceId)
        {
            logger.LogWarning("Token panelu {DeviceId} użyty przez {Reported}", deviceId, hello.DeviceId);
            return false;
        }
        await WithPanelAsync(panel => panel.ConnectAsync(deviceId, hello, connection, cancellationToken));
        _introduced = true;
        return true;
    }

    private async Task WithPanelAsync(Func<PanelService, Task> action)
    {
        await using var scope = scopes.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<PanelService>());
    }
}
