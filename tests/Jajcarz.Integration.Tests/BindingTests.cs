using Jajcarz.Domain.Products;
using Jajcarz.Integration.Tests.Support;

namespace Jajcarz.Integration.Tests;

public class BindingTests : IAsyncLifetime
{
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(1);

    private readonly TestApp _app = new();
    private FakePanel _panel = null!;
    private Guid _milk;

    public async Task InitializeAsync()
    {
        await _app.InitializeAsync();
        _panel = await _app.ConnectIntroducedAsync();
        _milk = await _app.AddProductAsync("Mleko");
        await _app.BindAsync(_milk, "0.0");
        await _panel.ExpectAsync("snapshot");
        _app.Clock.Advance(Step);
    }

    public async Task DisposeAsync()
    {
        await _panel.DisposeAsync();
        await ((IAsyncLifetime)_app).DisposeAsync();
    }

    [Fact]
    public async Task BindingKeepsProductStateAndLastAuthor()
    {
        await _app.ChangeFromWebAsync(_milk, ProductState.LOW);
        await _panel.ExpectAsync("state_set");
        var before = await _app.FindProductAsync(_milk);
        _app.Clock.Advance(Step);

        await _app.BindAsync(_milk, "0.1");
        var snapshot = await _panel.ExpectAsync("snapshot");
        var after = await _app.FindProductAsync(_milk);

        Assert.Equal("LOW", snapshot.GetProperty("keys")[1].GetProperty("state").GetString());
        Assert.Equal(before!.ChangedBy, after!.ChangedBy);
        Assert.Equal(before.ChangedAt, after.ChangedAt);
        Assert.True(after.Rev > before.Rev);
    }

    [Fact]
    public async Task MovingSendsOneSnapshotAndFreesOldKey()
    {
        await _app.BindAsync(_milk, "0.1");

        var keys = (await _panel.ExpectAsync("snapshot")).GetProperty("keys");

        Assert.False(keys[0].GetProperty("assigned").GetBoolean());
        Assert.True(keys[1].GetProperty("assigned").GetBoolean());
        Assert.Null((await _app.FindKeyAsync("0.0"))!.ProductId);
        await _panel.ExpectSilenceAsync();
    }

    [Fact]
    public async Task BindingToTakenKeyReleasesPreviousProduct()
    {
        var eggs = await _app.AddProductAsync("Jajka");

        await _app.BindAsync(eggs, "0.0");
        await _panel.ExpectAsync("snapshot");

        Assert.Equal(eggs, (await _app.FindKeyAsync("0.0"))!.ProductId);
        Assert.NotNull(await _app.FindProductAsync(_milk));
    }

    [Fact]
    public async Task BindingAgainToSameKeyInvalidatesOldClicks()
    {
        await _app.BindAsync(_milk, "0.0");
        await _panel.ExpectAsync("snapshot");
        _app.Clock.Advance(Step);

        // Kliknięcie jest nowsze niż ostatnia zmiana stanu, ale starsze niż przypięcie.
        await _panel.ClickAsync("0.0", "OUT", seq: 1, ageMs: (long)(Step * 1.5).TotalMilliseconds);

        Assert.False((await _panel.ExpectAsync("ack")).GetProperty("accepted").GetBoolean());
    }

    [Fact]
    public async Task ClickFromBeforeBindingDoesNotReachNewProduct()
    {
        var eggs = await _app.AddProductAsync("Jajka");
        _app.Clock.Advance(Step);
        await _app.BindAsync(eggs, "0.1");
        await _panel.ExpectAsync("snapshot");
        _app.Clock.Advance(Step);

        // Kliknięcie jest nowsze niż utworzenie produktu, ale starsze niż przypięcie.
        await _panel.ClickAsync("0.1", "OUT", seq: 1, ageMs: (long)(Step * 1.5).TotalMilliseconds);
        var ack = await _panel.ExpectAsync("ack");

        Assert.False(ack.GetProperty("accepted").GetBoolean());
        Assert.Equal("OK", ack.GetProperty("state").GetString());
        Assert.Equal(ProductState.OK, (await _app.FindProductAsync(eggs))!.State);
    }

    [Fact]
    public async Task ClickFromBeforeRestartAndBindingDoesNotReachNewProduct()
    {
        var known = (await _app.FindProductAsync(_milk))!.Rev;
        var eggs = await _app.AddProductAsync("Jajka");
        await _app.BindAsync(eggs, "0.0");
        await _panel.ExpectAsync("snapshot");

        await _panel.ClickAfterRestartAsync("0.0", "OUT", seq: 1, baseRev: known);

        Assert.False((await _panel.ExpectAsync("ack")).GetProperty("accepted").GetBoolean());
    }

    [Fact]
    public async Task UnbindingKeepsProductAndClearsKey()
    {
        await _app.ChangeFromWebAsync(_milk, ProductState.OUT);
        await _panel.ExpectAsync("state_set");

        var unbound = await _app.UseProductsAsync(service => service.UnbindAsync(_milk, CancellationToken.None));
        var keys = (await _panel.ExpectAsync("snapshot")).GetProperty("keys");

        Assert.True(unbound);
        Assert.False(keys[0].GetProperty("assigned").GetBoolean());
        Assert.Equal(ProductState.OUT, (await _app.FindProductAsync(_milk))!.State);
    }

    [Fact]
    public async Task DeletingProductFreesKey()
    {
        var deleted = await _app.UseProductsAsync(service => service.DeleteAsync(_milk, CancellationToken.None));
        var keys = (await _panel.ExpectAsync("snapshot")).GetProperty("keys");

        Assert.True(deleted);
        Assert.False(keys[0].GetProperty("assigned").GetBoolean());
        Assert.Null(await _app.FindProductAsync(_milk));
    }

    [Fact]
    public async Task BindingWhileOfflineReachesPanelOnNextConnection()
    {
        await _panel.DisposeAsync();
        var eggs = await _app.AddProductAsync("Jajka");

        await _app.BindAsync(eggs, "0.3");
        await using var reconnected = await _app.ConnectAsync();
        await reconnected.HelloAsync();
        var keys = (await reconnected.ExpectAsync("snapshot")).GetProperty("keys");

        Assert.True(keys[3].GetProperty("assigned").GetBoolean());
    }

    [Fact]
    public async Task BindingUnknownKeyFails()
    {
        Assert.False(await _app.BindAsync(_milk, "9.9"));
        await _panel.ExpectSilenceAsync();
    }
}
