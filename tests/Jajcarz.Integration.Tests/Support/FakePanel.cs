using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Jajcarz.Integration.Tests.Support;

/// <summary>Symulator ESP32: wysyła dokładnie takie teksty, jakie generuje firmware.</summary>
public sealed class FakePanel(WebSocket socket) : IAsyncDisposable
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SilenceWindow = TimeSpan.FromMilliseconds(200);

    private Task<WebSocketReceiveResult>? _pendingReceive;
    private readonly byte[] _buffer = new byte[16 * 1024];

    public WebSocketState State => socket.State;

    public Task SendAsync(string text) =>
        socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, CancellationToken.None);

    public Task HelloAsync(string deviceId = TestApp.DeviceId, int keys = 4) =>
        SendAsync($$"""{"v":1,"t":"hello","device_id":"{{deviceId}}","fw":"v0.3.0","segments":[{"seg":0,"keys":{{keys}}}]}""");

    public Task HelloWithSegmentsAsync(string segments) =>
        SendAsync($$"""{"v":1,"t":"hello","device_id":"{{TestApp.DeviceId}}","fw":"v0.3.0","segments":{{segments}}}""");

    public Task ClickAsync(string addr, string state, long seq, long ageMs) =>
        SendAsync($$"""{"v":1,"t":"state_change","seq":{{seq}},"addr":"{{addr}}","state":"{{state}}","age_ms":{{ageMs}}}""");

    public Task ClickAfterRestartAsync(string addr, string state, long seq, long baseRev) =>
        SendAsync($$"""{"v":1,"t":"state_change","seq":{{seq}},"addr":"{{addr}}","state":"{{state}}","base_rev":{{baseRev}}}""");

    public Task PressUnassignedAsync(string addr) =>
        SendAsync($$"""{"v":1,"t":"unassigned_press","addr":"{{addr}}"}""");

    public async Task<JsonElement> ReceiveAsync()
    {
        using var cancellation = new CancellationTokenSource(ReceiveTimeout);
        var receive = NextReceive();
        var result = await receive.WaitAsync(cancellation.Token);
        _pendingReceive = null;
        return JsonDocument.Parse(_buffer.AsMemory(0, result.Count)).RootElement.Clone();
    }

    /// <summary>Odbiera wiadomość i sprawdza jej typ.</summary>
    public async Task<JsonElement> ExpectAsync(string type)
    {
        var message = await ReceiveAsync();
        Assert.Equal(type, message.GetProperty("t").GetString());
        return message;
    }

    public async Task ExpectSilenceAsync()
    {
        var receive = NextReceive();
        var finished = await Task.WhenAny(receive, Task.Delay(SilenceWindow));
        Assert.NotSame(receive, finished);
    }

    public async Task<WebSocketCloseStatus?> ExpectClosedAsync()
    {
        using var cancellation = new CancellationTokenSource(ReceiveTimeout);
        var result = await NextReceive().WaitAsync(cancellation.Token);
        _pendingReceive = null;
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
        return result.CloseStatus;
    }

    public async ValueTask DisposeAsync()
    {
        socket.Abort();
        socket.Dispose();
        await Task.CompletedTask;
    }

    private Task<WebSocketReceiveResult> NextReceive() =>
        _pendingReceive ??= socket.ReceiveAsync(new ArraySegment<byte>(_buffer), CancellationToken.None);
}
