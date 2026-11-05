using System.Net.WebSockets;
using Jajcarz.Application.Panels;
using Microsoft.AspNetCore.WebSockets;

namespace Jajcarz.Server.Transport;

public static class PanelSocketEndpoint
{
    private const string BearerPrefix = "Bearer ";

    // Ping panelu obsługuje .NET i nie dociera on do aplikacji, a panel bez prądu nie zamyka
    // połączenia. Dlatego zerwanie wykrywa serwer, wysyłając własny ping.
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan KeepAliveTimeout = TimeSpan.FromSeconds(30);

    public static void MapPanelSocket(this IEndpointRouteBuilder routes) => routes.Map("/ws/device", HandleAsync);

    private static async Task HandleAsync(
        HttpContext context, IServiceScopeFactory scopes, ILogger<PanelConnection> logger)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        var deviceId = await AuthenticateAsync(context, scopes);
        if (deviceId is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext
        {
            KeepAliveInterval = KeepAliveInterval,
            KeepAliveTimeout = KeepAliveTimeout,
        });
        await RunConnectionAsync(deviceId, socket, scopes, logger, context.RequestAborted);
    }

    private static async Task RunConnectionAsync(
        string deviceId, WebSocket socket, IServiceScopeFactory scopes, ILogger logger, CancellationToken cancellationToken)
    {
        var connection = new PanelConnection(socket, logger);
        var session = new PanelSession(deviceId, connection, scopes, logger);
        var pump = connection.PumpOutboxAsync(cancellationToken);
        var closeStatus = WebSocketCloseStatus.NormalClosure;
        try
        {
            closeStatus = await session.RunAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException)
        {
            logger.LogInformation("Połączenie z panelem {DeviceId} przerwane", deviceId);
        }

        await session.EndAsync();
        connection.CompleteOutbox();
        try
        {
            await pump;
            await connection.CloseAsync(closeStatus);
        }
        catch (Exception exception) when (exception is OperationCanceledException or WebSocketException)
        {
            logger.LogDebug("Panel {DeviceId} rozłączył się przed końcem wysyłki", deviceId);
        }
    }

    private static async Task<string?> AuthenticateAsync(HttpContext context, IServiceScopeFactory scopes)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.Ordinal))
        {
            return null;
        }
        await using var scope = scopes.CreateAsyncScope();
        var panel = scope.ServiceProvider.GetRequiredService<PanelService>();
        return await panel.AuthenticateAsync(header[BearerPrefix.Length..], context.RequestAborted);
    }
}
