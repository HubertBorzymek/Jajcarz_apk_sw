namespace Jajcarz.Domain.Products;

/// <summary>Reguły pól produktu wpisywanych przez użytkownika.</summary>
public static class ProductFields
{
    /// <summary>Zwraca pola bez zbędnych spacji albo null, gdy nazwa lub kategoria są niepoprawne.</summary>
    public static (string Name, string? Category)? Normalize(string? name, string? category)
    {
        var trimmedName = name?.Trim() ?? "";
        var trimmedCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        var valid = trimmedName.Length is > 0 and <= Product.MaxNameLength
            && (trimmedCategory?.Length ?? 0) <= Product.MaxCategoryLength;
        return valid ? (trimmedName, trimmedCategory) : null;
    }
}
