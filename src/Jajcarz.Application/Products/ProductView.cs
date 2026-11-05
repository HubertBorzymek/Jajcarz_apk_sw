using Jajcarz.Domain.Products;

namespace Jajcarz.Application.Products;

/// <summary>Produkt widziany w aplikacji, razem z adresem jego klawisza.</summary>
public sealed record ProductView(
    Guid Id,
    string Name,
    string? Category,
    ProductState State,
    long Rev,
    string ChangedBy,
    DateTimeOffset ChangedAt,
    string? Addr)
{
    public static ProductView From(Product product, string? addr) => new(
        product.Id,
        product.Name,
        product.Category,
        product.State,
        product.Rev,
        product.ChangedBy,
        product.ChangedAt,
        addr);
}
