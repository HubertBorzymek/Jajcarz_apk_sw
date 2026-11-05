namespace Jajcarz.Domain.Products;

/// <summary>Produkt i jego stan. Klawisz panelu jest do niego tylko przypięty.</summary>
public sealed class Product
{
    public const int MaxNameLength = 80;
    public const int MaxCategoryLength = 40;

    private Product()
    {
        Name = "";
        ChangedBy = "";
    }

    public Product(Guid id, string name, string? category, string createdBy, DateTimeOffset now, long rev)
    {
        Id = id;
        Name = name;
        Category = category;
        State = ProductState.OK;
        Rev = rev;
        ChangedBy = createdBy;
        ChangedAt = now;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string? Category { get; private set; }

    public ProductState State { get; private set; }

    public long Rev { get; private set; }

    public string ChangedBy { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    public string? LastChangeKey { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void Apply(StateChange change, long rev)
    {
        State = change.State;
        Rev = rev;
        ChangedBy = change.Actor;
        ChangedAt = change.OccurredAt;
        LastChangeKey = change.ChangeKey;
    }

    /// <summary>
    /// Nowy rev unieważnia zmiany, które panel zapamiętał dla tego klawisza przed przypięciem.
    /// Autor i czas zostają, bo przypięcie nie jest zmianą stanu.
    /// </summary>
    public void MarkBound(long rev)
    {
        Rev = rev;
        LastChangeKey = null;
    }
}
