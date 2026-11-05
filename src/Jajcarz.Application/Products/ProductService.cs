using Jajcarz.Application.Abstractions;
using Jajcarz.Application.Panels;
using Jajcarz.Domain.Panels;
using Jajcarz.Domain.Products;

namespace Jajcarz.Application.Products;

/// <summary>Zmiana stanu zgłoszona z aplikacji. AgeMs opisuje zmianę czekającą w kolejce offline.</summary>
public sealed record ProductStateRequest(Guid ProductId, ProductState State, long AgeMs = 0, string? ChangeKey = null);

public sealed record ProductStateResult(Guid ProductId, bool Accepted, ProductState State, long Rev);

/// <summary>Operacje na produktach i ich przypięciach do klawiszy.</summary>
public sealed class ProductService(
    IStore store, PanelLinks links, SnapshotBuilder snapshots, ProductEvents events, TimeProvider clock)
{
    public async Task<IReadOnlyList<ProductView>> ListAsync(CancellationToken cancellationToken)
    {
        var products = await store.ListProductsAsync(cancellationToken);
        var keys = await store.ListBoundKeysAsync(cancellationToken);
        var addrs = keys.ToDictionary(key => key.ProductId!.Value, key => key.Addr);
        return products.Select(product => ProductView.From(product, addrs.GetValueOrDefault(product.Id))).ToList();
    }

    public async Task<ProductView?> FindAsync(Guid productId, CancellationToken cancellationToken)
    {
        var product = await store.FindProductAsync(productId, cancellationToken);
        return product is null ? null : await ViewAsync(product, cancellationToken);
    }

    public async Task<Product> CreateAsync(string name, string? category, string actor, CancellationToken cancellationToken)
    {
        await using var write = await store.BeginWriteAsync(cancellationToken);
        var rev = await store.NextRevAsync(cancellationToken);
        var product = new Product(Guid.NewGuid(), name, category, actor, clock.GetUtcNow(), rev);
        store.Add(product);
        await write.CommitAsync(cancellationToken);
        events.Publish(new ProductUpdated(ProductView.From(product, null)));
        return product;
    }

    /// <summary>Zmienia nazwę i kategorię. Zwraca null, gdy produktu nie ma.</summary>
    public async Task<ProductView?> UpdateAsync(
        Guid productId, string name, string? category, CancellationToken cancellationToken)
    {
        await using var write = await store.BeginWriteAsync(cancellationToken);
        var product = await store.FindProductAsync(productId, cancellationToken);
        if (product is null)
        {
            return null;
        }
        product.Update(name, category);
        await write.CommitAsync(cancellationToken);
        var view = await ViewAsync(product, cancellationToken);
        events.Publish(new ProductUpdated(view));
        return view;
    }

    public async Task<ProductStateResult?> ChangeStateAsync(
        ProductStateRequest request, string actor, CancellationToken cancellationToken)
    {
        var results = await ChangeStatesAsync([request], actor, cancellationToken);
        return results.SingleOrDefault();
    }

    /// <summary>
    /// Przy kilku zmianach na jednym panelu wysyła jeden snapshot. Kolejka panelu mieści
    /// 8 wiadomości, a utracony state_set czekałby na następne połączenie.
    /// </summary>
    public async Task<IReadOnlyList<ProductStateResult>> ChangeStatesAsync(
        IReadOnlyCollection<ProductStateRequest> requests, string actor, CancellationToken cancellationToken)
    {
        await using var write = await store.BeginWriteAsync(cancellationToken);
        var results = new List<ProductStateResult>();
        var changedOnKeys = new List<(PanelKey Key, Product Product)>();
        var changed = new List<ProductView>();

        foreach (var request in requests)
        {
            var product = await store.FindProductAsync(request.ProductId, cancellationToken);
            if (product is null)
            {
                continue;
            }
            var key = await store.FindKeyOfProductAsync(product.Id, cancellationToken);
            var change = new StateChange(request.State, actor, clock.GetUtcNow(), request.AgeMs, ChangeKey: request.ChangeKey);
            var outcome = ConflictPolicy.Decide(product, key?.BoundAt, change);
            if (outcome == ChangeOutcome.Accepted)
            {
                product.Apply(change, await store.NextRevAsync(cancellationToken));
                changed.Add(ProductView.From(product, key?.Addr));
                if (key is not null)
                {
                    changedOnKeys.Add((key, product));
                }
            }
            results.Add(new ProductStateResult(product.Id, outcome != ChangeOutcome.Rejected, product.State, product.Rev));
        }

        await write.CommitAsync(cancellationToken);
        changed.ForEach(view => events.Publish(new ProductUpdated(view)));
        await NotifyPanelsAsync(changedOnKeys, cancellationToken);
        return results;
    }

    /// <summary>Przypina produkt do klawisza. Stan zostaje przy produkcie, a klawisz go przejmuje.</summary>
    public async Task<bool> BindAsync(Guid productId, string deviceId, string addr, CancellationToken cancellationToken)
    {
        await using var write = await store.BeginWriteAsync(cancellationToken);
        var product = await store.FindProductAsync(productId, cancellationToken);
        var key = await store.FindKeyAsync(deviceId, addr, cancellationToken);
        if (product is null || key is null)
        {
            return false;
        }

        var previous = await store.FindKeyOfProductAsync(productId, cancellationToken);
        previous?.Unbind();
        await store.SaveAsync(cancellationToken);   // indeks unikalny: najpierw zwolnij stary klawisz

        key.Bind(productId, clock.GetUtcNow());
        product.MarkBound(await store.NextRevAsync(cancellationToken));
        await write.CommitAsync(cancellationToken);
        events.Publish(new ProductUpdated(ProductView.From(product, addr)));

        await SendSnapshotsAsync([deviceId, .. previous is null ? [] : new[] { previous.DeviceId }], cancellationToken);
        return true;
    }

    public async Task<bool> UnbindAsync(Guid productId, CancellationToken cancellationToken)
    {
        await using var write = await store.BeginWriteAsync(cancellationToken);
        var key = await store.FindKeyOfProductAsync(productId, cancellationToken);
        if (key is null)
        {
            return false;
        }
        key.Unbind();
        await write.CommitAsync(cancellationToken);
        if (await store.FindProductAsync(productId, cancellationToken) is { } product)
        {
            events.Publish(new ProductUpdated(ProductView.From(product, null)));
        }

        await SendSnapshotsAsync([key.DeviceId], cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid productId, CancellationToken cancellationToken)
    {
        await using var write = await store.BeginWriteAsync(cancellationToken);
        var product = await store.FindProductAsync(productId, cancellationToken);
        if (product is null)
        {
            return false;
        }
        var key = await store.FindKeyOfProductAsync(productId, cancellationToken);
        key?.Unbind();
        store.Remove(product);
        await write.CommitAsync(cancellationToken);
        events.Publish(new ProductRemoved(productId));

        if (key is not null)
        {
            await SendSnapshotsAsync([key.DeviceId], cancellationToken);
        }
        return true;
    }

    private async Task<ProductView> ViewAsync(Product product, CancellationToken cancellationToken)
    {
        var key = await store.FindKeyOfProductAsync(product.Id, cancellationToken);
        return ProductView.From(product, key?.Addr);
    }

    private async Task NotifyPanelsAsync(List<(PanelKey Key, Product Product)> changes, CancellationToken cancellationToken)
    {
        foreach (var panel in changes.GroupBy(change => change.Key.DeviceId))
        {
            if (panel.Count() > 1)
            {
                await SendSnapshotsAsync([panel.Key], cancellationToken);
            }
            else
            {
                var (key, product) = panel.Single();
                links.Send(panel.Key, StateSetMessage.For(key.Addr, product));
            }
        }
    }

    private async Task SendSnapshotsAsync(IEnumerable<string> deviceIds, CancellationToken cancellationToken)
    {
        foreach (var deviceId in deviceIds.Distinct().Where(links.IsConnected))
        {
            if (await store.FindDeviceAsync(deviceId, cancellationToken) is { } device)
            {
                links.Send(deviceId, await snapshots.BuildAsync(device, cancellationToken));
            }
        }
    }
}
