namespace Jajcarz.Domain.Products;

/// <summary>
/// Zgłoszona zmiana stanu. Czas powstania opisuje AgeMs, a gdy jest nieznany, czyli po
/// restarcie panelu, BaseRev. ChangeKey rozpoznaje ponowioną wysyłkę tej samej zmiany.
/// </summary>
public sealed record StateChange(
    ProductState State,
    string Actor,
    DateTimeOffset ReceivedAt,
    long? AgeMs,
    long? BaseRev = null,
    string? ChangeKey = null)
{
    public DateTimeOffset OccurredAt => ReceivedAt - TimeSpan.FromMilliseconds(AgeMs ?? 0);
}
