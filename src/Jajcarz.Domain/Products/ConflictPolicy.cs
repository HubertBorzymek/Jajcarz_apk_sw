namespace Jajcarz.Domain.Products;

public enum ChangeOutcome
{
    Accepted,
    Rejected,

    /// <summary>Ta sama zmiana została już zapisana, tylko nie dotarło jej potwierdzenie.</summary>
    Duplicate,
}

/// <summary>Reguły konfliktów z docs/PROTOCOL.md.</summary>
public static class ConflictPolicy
{
    /// <param name="boundAt">
    /// Czas przypięcia produktu do klawisza. Kliknięcie sprzed przypięcia dotyczyło innego
    /// produktu, dlatego przegrywa nawet wtedy, gdy produkt dawno się nie zmieniał.
    /// </param>
    public static ChangeOutcome Decide(Product product, DateTimeOffset? boundAt, StateChange change)
    {
        if (change.ChangeKey is not null
            && change.ChangeKey == product.LastChangeKey
            && change.State == product.State)
        {
            return ChangeOutcome.Duplicate;
        }
        if (change.AgeMs is { } age)
        {
            return age >= 0 && change.OccurredAt > Latest(product.ChangedAt, boundAt)
                ? ChangeOutcome.Accepted
                : ChangeOutcome.Rejected;
        }
        return change.BaseRev >= product.Rev ? ChangeOutcome.Accepted : ChangeOutcome.Rejected;
    }

    private static DateTimeOffset Latest(DateTimeOffset changedAt, DateTimeOffset? boundAt) =>
        boundAt > changedAt ? boundAt.Value : changedAt;
}
