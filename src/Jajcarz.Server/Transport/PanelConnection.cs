using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Jajcarz.Application.Panels;

namespace Jajcarz.Server.Transport;

/// <summary>
/// Połączenie WebSocket z panelem. Wiadomości wychodzące czekają w kolejce, więc Send nie
/// blokuje zapisu, a kolejność wysyłki jest taka sama jak kolejność zmian.
/// </summary>
public sealed class PanelConnection(WebSocket socket, ILogger logger) : IPanelChannel
{
    private const int OutboxCapacity = 64;
    private const int ReceiveBufferSize = 4096;
    private const int MaxMessageSize = 64 * 1024;

    private readonly Channel<PanelMessage> _outbox = Channel.CreateBounded<PanelMessage>(
        new BoundedChannelOptions(OutboxCapacity) { SingleReader = true });

    /// <summary>Przepełnienie oznacza, że panel nie odbiera. Po nowym połączeniu dostanie snapshot.</summary>
    public void Send(PanelMessage message)
    {
        if (!_outbox.Writer.TryWrite(message))
        {
            logger.LogWarning("Pełna kolejka do panelu, zamykam połączenie");
            Abort();
        }
    }

    public void Abort() => socket.Abort();

    public async Task PumpOutboxAsync(CancellationToken cancellationToken)
    {
        await foreach (var message in _outbox.Reader.ReadAllAsync(cancellationToken))
        {
            await socket.SendAsync(PanelJson.Serialize(message), WebSocketMessageType.Text, true, cancellationToken);
        }
    }

    public void CompleteOutbox() => _outbox.Writer.TryComplete();

    /// <summary>Gniazdo nie pozwala na równoległą wysyłkę, więc zamknięcie następuje po opróżnieniu kolejki.</summary>
    public async Task CloseAsync(WebSocketCloseStatus status)
    {
        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await socket.CloseOutputAsync(status, null, CancellationToken.None);
        }
    }

    /// <summary>Zwraca kolejną wiadomość tekstową albo null, gdy panel kończy połączenie.</summary>
    public async Task<string?> ReceiveAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];
        using var message = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }
            message.Write(buffer, 0, result.Count);
            if (message.Length > MaxMessageSize)
            {
                throw new WebSocketException(WebSocketError.Faulted, "Za długa wiadomość od panelu");
            }
            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
            }
        }
    }
}
