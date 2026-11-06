namespace Jajcarz.Domain.Panels;

/// <summary>Panel zarejestrowany w aplikacji. Token jest przechowywany tylko jako skrót.</summary>
public sealed class Device
{
    private Device()
    {
        Id = "";
        TokenHash = "";
        Config = PanelConfig.Default;
    }

    public Device(string id, string tokenHash)
    {
        Id = id;
        TokenHash = tokenHash;
        Config = PanelConfig.Default;
    }

    public string Id { get; private set; }

    public string TokenHash { get; private set; }

    public string? Firmware { get; private set; }

    public DateTimeOffset? LastSeenAt { get; private set; }

    public PanelConfig Config { get; private set; }

    public void ReportFirmware(string firmware) => Firmware = firmware;

    public void MarkSeen(DateTimeOffset now) => LastSeenAt = now;

    public void Configure(PanelConfig config) => Config = config;

    public void ReplaceToken(string tokenHash) => TokenHash = tokenHash;
}
