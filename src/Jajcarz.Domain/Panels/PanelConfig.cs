using Jajcarz.Domain.Products;

namespace Jajcarz.Domain.Panels;

/// <summary>Wygląd panelu: wspólna jasność i kolor każdego stanu jako [r, g, b].</summary>
public sealed record PanelConfig(int Brightness, IReadOnlyDictionary<ProductState, int[]> Colors)
{
    public static readonly PanelConfig Default = new(51, new Dictionary<ProductState, int[]>
    {
        [ProductState.OK] = [0, 255, 0],
        [ProductState.LOW] = [255, 120, 0],
        [ProductState.OUT] = [255, 0, 0],
    });

    public bool IsValid() =>
        Brightness is >= 0 and <= 255
        && Enum.GetValues<ProductState>().All(state =>
            Colors.TryGetValue(state, out var color)
            && color.Length == 3
            && color.All(channel => channel is >= 0 and <= 255));
}
