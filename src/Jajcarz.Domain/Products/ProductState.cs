namespace Jajcarz.Domain.Products;

/// <summary>Stan produktu. Nazwy są takie same jak w protokole panelu.</summary>
public enum ProductState
{
    OK,
    LOW,
    OUT,
}

public static class ProductStates
{
    /// <summary>Odczytuje stan z protokołu. Enum.TryParse przyjąłby też liczby zapisane jako tekst.</summary>
    public static bool TryParse(string? text, out ProductState state)
    {
        switch (text)
        {
            case "OK":
                state = ProductState.OK;
                return true;
            case "LOW":
                state = ProductState.LOW;
                return true;
            case "OUT":
                state = ProductState.OUT;
                return true;
            default:
                state = default;
                return false;
        }
    }
}
