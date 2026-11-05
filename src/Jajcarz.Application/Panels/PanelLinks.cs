using System.Collections.Concurrent;

namespace Jajcarz.Application.Panels;

/// <summary>Kanał do połączonego panelu. Send nie czeka na wysyłkę, więc można go wołać w trakcie zapisu.</summary>
public interface IPanelChannel
{
    void Send(PanelMessage message);

    void Abort();
}

/// <summary>Połączone panele. Stan połączenia ginie z procesem, dlatego nie jest zapisywany w bazie.</summary>
public sealed class PanelLinks
{
    private readonly ConcurrentDictionary<string, IPanelChannel> _channels = new();

    /// <summary>Panel po zerwaniu sieci łączy się ponownie, zanim serwer zauważy koniec starego połączenia.</summary>
    public void Attach(string deviceId, IPanelChannel channel)
    {
        if (_channels.TryGetValue(deviceId, out var previous) && !ReferenceEquals(previous, channel))
        {
            previous.Abort();
        }
        _channels[deviceId] = channel;
    }

    public void Detach(string deviceId, IPanelChannel channel) =>
        _channels.TryRemove(new KeyValuePair<string, IPanelChannel>(deviceId, channel));

    public bool IsConnected(string deviceId) => _channels.ContainsKey(deviceId);

    /// <summary>Niepołączony panel dostanie aktualny stan w snapshot przy następnym połączeniu.</summary>
    public void Send(string deviceId, PanelMessage message)
    {
        if (_channels.TryGetValue(deviceId, out var channel))
        {
            channel.Send(message);
        }
    }
}
