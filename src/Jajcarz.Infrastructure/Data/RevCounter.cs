namespace Jajcarz.Infrastructure.Data;

/// <summary>Jeden wiersz z licznikiem rev wspólnym dla wszystkich produktów.</summary>
public sealed class RevCounter
{
    public const int SingleId = 1;

    public int Id { get; set; }

    public long Value { get; set; }
}
