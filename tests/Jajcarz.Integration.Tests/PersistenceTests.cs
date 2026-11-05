using Jajcarz.Domain.Products;
using Jajcarz.Integration.Tests.Support;

namespace Jajcarz.Integration.Tests;

public class PersistenceTests : IAsyncLifetime
{
    private readonly TestApp _app = new();

    public Task InitializeAsync() => _app.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_app).DisposeAsync();

    [Fact]
    public async Task StateAndRevSurviveRestart()
    {
        await using (var panel = await _app.ConnectIntroducedAsync())
        {
            var milk = await _app.AddProductAsync("Mleko");
            await _app.BindAsync(milk, "0.0");
            await panel.ExpectAsync("snapshot");
            _app.Clock.Advance(TimeSpan.FromMinutes(1));
            await panel.ClickAsync("0.0", "OUT", seq: 1, ageMs: 100);
            await panel.ExpectAsync("ack");
        }

        await using var restarted = _app.Restarted();
        await using var reconnected = await restarted.ConnectAsync();
        await reconnected.HelloAsync();
        var snapshot = await reconnected.ExpectAsync("snapshot");
        var key = snapshot.GetProperty("keys")[0];

        Assert.Equal("OUT", key.GetProperty("state").GetString());
        var rev = key.GetProperty("rev").GetInt64();
        Assert.True(rev > 0);
        Assert.Equal(rev, snapshot.GetProperty("rev").GetInt64());
    }

    [Fact]
    public async Task RevDoesNotGoBackAfterRestart()
    {
        await _app.AddProductAsync("Mleko");
        var before = await _app.UseStoreAsync(store => store.CurrentRevAsync(CancellationToken.None));

        await using var restarted = _app.Restarted();
        var product = await restarted.UseProductsAsync(service =>
            service.CreateAsync("Jajka", null, TestApp.User, CancellationToken.None));

        Assert.True(product.Rev > before);
    }

    [Fact]
    public async Task ConcurrentChangesGetDifferentRevs()
    {
        var ids = new List<Guid>();
        for (var i = 0; i < 8; i++)
        {
            ids.Add(await _app.AddProductAsync($"Produkt {i}"));
        }
        _app.Clock.Advance(TimeSpan.FromMinutes(1));

        var results = await Task.WhenAll(ids.Select(id => _app.ChangeFromWebAsync(id, ProductState.LOW)));

        Assert.Equal(ids.Count, results.Select(result => result!.Rev).Distinct().Count());
    }
}
