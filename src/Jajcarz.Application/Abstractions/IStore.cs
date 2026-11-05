using Jajcarz.Domain.Panels;
using Jajcarz.Domain.Products;

namespace Jajcarz.Application.Abstractions;

/// <summary>Trwałe dane aplikacji. Warstwa aplikacji nie zna bazy ani EF Core.</summary>
public interface IStore
{
    /// <summary>
    /// Rozpoczyna zapis. Tylko jeden zapis może trwać naraz, a powiadomienia wysłane przed
    /// zamknięciem zakresu docierają do panelu w tej samej kolejności co zmiany w bazie.
    /// </summary>
    Task<IWriteScope> BeginWriteAsync(CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);

    /// <summary>Podnosi licznik zmian i zwraca nową wartość. Licznik nigdy się nie cofa.</summary>
    Task<long> NextRevAsync(CancellationToken cancellationToken);

    Task<long> CurrentRevAsync(CancellationToken cancellationToken);

    void Add(Device device);

    void Add(PanelKey key);

    void Add(Product product);

    void Remove(Product product);

    Task<Device?> FindDeviceAsync(string id, CancellationToken cancellationToken);

    Task<Device?> FindDeviceByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task<Product?> FindProductAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Product>> ListProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<PanelKey?> FindKeyAsync(string deviceId, string addr, CancellationToken cancellationToken);

    Task<PanelKey?> FindKeyOfProductAsync(Guid productId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PanelKey>> ListKeysAsync(string deviceId, CancellationToken cancellationToken);
}

public interface IWriteScope : IAsyncDisposable
{
    /// <summary>Zapisuje zmiany i zatwierdza transakcję. Blokada zapisu trwa do zamknięcia zakresu.</summary>
    Task CommitAsync(CancellationToken cancellationToken);
}
