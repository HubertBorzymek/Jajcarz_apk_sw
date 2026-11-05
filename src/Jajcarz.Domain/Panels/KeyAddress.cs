namespace Jajcarz.Domain.Panels;

/// <summary>Adres klawisza w postaci "segment.klawisz", np. "0.2".</summary>
public static class KeyAddress
{
    public static string Format(int segment, int key) => $"{segment}.{key}";

    /// <summary>Klucz sortowania, bo jako tekst "0.10" byłoby przed "0.2".</summary>
    public static (int Segment, int Key) Order(string addr)
    {
        var parts = addr.Split('.');
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }
}
