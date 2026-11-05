using Jajcarz.Application.Abstractions;
using Jajcarz.Domain.Panels;
using Jajcarz.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Jajcarz.Infrastructure.Data;

/// <summary>Blokada zapisu wspólna dla całego procesu. Aplikacja działa w jednym egzemplarzu.</summary>
public sealed class WriteGate
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
}

public sealed class EfStore(JajcarzDbContext db, WriteGate gate) : IStore
{
    public async Task<IWriteScope> BeginWriteAsync(CancellationToken cancellationToken)
    {
        await gate.Semaphore.WaitAsync(cancellationToken);
        try
        {
            var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            return new WriteScope(db, transaction, gate);
        }
        catch
        {
            gate.Semaphore.Release();
            throw;
        }
    }

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    public async Task<long> NextRevAsync(CancellationToken cancellationToken)
    {
        await Counter().ExecuteUpdateAsync(
            setters => setters.SetProperty(counter => counter.Value, counter => counter.Value + 1),
            cancellationToken);
        return await CurrentRevAsync(cancellationToken);
    }

    public Task<long> CurrentRevAsync(CancellationToken cancellationToken) =>
        Counter().Select(counter => counter.Value).SingleAsync(cancellationToken);

    public void Add(Device device) => db.Devices.Add(device);

    public void Add(PanelKey key) => db.PanelKeys.Add(key);

    public void Add(Product product) => db.Products.Add(product);

    public void Remove(Product product) => db.Products.Remove(product);

    public Task<Device?> FindDeviceAsync(string id, CancellationToken cancellationToken) =>
        db.Devices.SingleOrDefaultAsync(device => device.Id == id, cancellationToken);

    public Task<Device?> FindDeviceByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.Devices.SingleOrDefaultAsync(device => device.TokenHash == tokenHash, cancellationToken);

    public Task<Product?> FindProductAsync(Guid id, CancellationToken cancellationToken) =>
        db.Products.SingleOrDefaultAsync(product => product.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Product>> ListProductsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await db.Products.Where(product => ids.Contains(product.Id)).ToListAsync(cancellationToken);

    public Task<PanelKey?> FindKeyAsync(string deviceId, string addr, CancellationToken cancellationToken) =>
        db.PanelKeys.SingleOrDefaultAsync(key => key.DeviceId == deviceId && key.Addr == addr, cancellationToken);

    public Task<PanelKey?> FindKeyOfProductAsync(Guid productId, CancellationToken cancellationToken) =>
        db.PanelKeys.SingleOrDefaultAsync(key => key.ProductId == productId, cancellationToken);

    public async Task<IReadOnlyList<PanelKey>> ListKeysAsync(string deviceId, CancellationToken cancellationToken) =>
        await db.PanelKeys.Where(key => key.DeviceId == deviceId).ToListAsync(cancellationToken);

    private IQueryable<RevCounter> Counter() =>
        db.RevCounters.AsNoTracking().Where(counter => counter.Id == RevCounter.SingleId);

    private sealed class WriteScope(JajcarzDbContext db, IDbContextTransaction transaction, WriteGate gate) : IWriteScope
    {
        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await transaction.DisposeAsync();
            gate.Semaphore.Release();
        }
    }
}
