using Jajcarz.Application.Abstractions;
using Jajcarz.Domain.Panels;

namespace Jajcarz.Application.Panels;

public sealed class SnapshotBuilder(IStore store)
{
    /// <summary>
    /// Zawiera tylko klawisze z ostatniego hello. Panel odrzuca cały snapshot, gdy dostanie
    /// więcej klawiszy, niż ma miejsca, więc klawisze odpiętych modułów tu nie trafiają.
    /// </summary>
    public async Task<SnapshotMessage> BuildAsync(Device device, CancellationToken cancellationToken)
    {
        var keys = (await store.ListKeysAsync(device.Id, cancellationToken))
            .Where(key => key.Present)
            .OrderBy(key => KeyAddress.Order(key.Addr))
            .ToList();
        var productIds = keys.Where(key => key.ProductId is not null).Select(key => key.ProductId!.Value).ToList();
        var products = (await store.ListProductsAsync(productIds, cancellationToken)).ToDictionary(product => product.Id);

        var items = keys
            .Select(key => key.ProductId is { } id && products.TryGetValue(id, out var product)
                ? new SnapshotKey(key.Addr, true, product.State, product.Rev)
                : new SnapshotKey(key.Addr, false))
            .ToList();
        return new SnapshotMessage(await store.CurrentRevAsync(cancellationToken), items, device.Config);
    }
}
