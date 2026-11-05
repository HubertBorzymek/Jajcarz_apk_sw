using System.Globalization;
using Jajcarz.Domain.Panels;
using Jajcarz.Domain.Products;

namespace Jajcarz.Application.Panels;

// Wiadomości z docs/PROTOCOL.md. Nazwy pól zamienia na format protokołu warstwa transportu.

public sealed record PanelSegment(int Seg, int Keys);

public sealed record PanelHello(string DeviceId, string Fw, IReadOnlyList<PanelSegment> Segments);

public sealed record PanelStateChange(long Seq, string Addr, string? State = null, long? AgeMs = null, long? BaseRev = null);

public sealed record PanelUnassignedPress(string Addr);

/// <summary>Wiadomość wysyłana do panelu.</summary>
public abstract record PanelMessage(string T)
{
    public const int ProtocolVersion = 1;

    public int V => ProtocolVersion;
}

public sealed record SnapshotKey(string Addr, bool Assigned, ProductState? State = null, long? Rev = null);

public sealed record SnapshotMessage(long Rev, IReadOnlyList<SnapshotKey> Keys, PanelConfig Config)
    : PanelMessage("snapshot");

public sealed record AckMessage(long Seq, string Addr, bool Accepted, ProductState State, long Rev)
    : PanelMessage("ack");

public sealed record StateSetMessage(string Addr, ProductState State, long Rev, string ChangedBy, string ChangedAt)
    : PanelMessage("state_set")
{
    public static StateSetMessage For(string addr, Product product) => new(
        addr,
        product.State,
        product.Rev,
        product.ChangedBy,
        product.ChangedAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
}
