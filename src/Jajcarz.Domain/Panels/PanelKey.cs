namespace Jajcarz.Domain.Panels;

/// <summary>
/// Klawisz panelu. Present mówi, czy zgłosił go ostatni hello. Klawisz odpiętego modułu
/// zostaje w bazie, żeby przepięcie modułu nie gubiło przypisań.
/// </summary>
public sealed class PanelKey
{
    private PanelKey()
    {
        DeviceId = "";
        Addr = "";
    }

    public PanelKey(string deviceId, string addr, bool present)
    {
        DeviceId = deviceId;
        Addr = addr;
        Present = present;
    }

    public string DeviceId { get; private set; }

    public string Addr { get; private set; }

    public bool Present { get; private set; }

    public Guid? ProductId { get; private set; }

    public DateTimeOffset? BoundAt { get; private set; }

    public void MarkPresent(bool present) => Present = present;

    public void Bind(Guid productId, DateTimeOffset now)
    {
        ProductId = productId;
        BoundAt = now;
    }

    public void Unbind()
    {
        ProductId = null;
        BoundAt = null;
    }
}
