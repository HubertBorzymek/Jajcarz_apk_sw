using System.Text.RegularExpressions;
using Jajcarz.Domain.Products;
using Jajcarz.Integration.Tests.Support;

namespace Jajcarz.Integration.Tests;

public class StateChangeTests : IAsyncLifetime
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
        await _app.BindAsync(_milk, "0.2");
        await _panel.ExpectAsync("snapshot");
        _app.Clock.Advance(Step);
    }

    public async Task DisposeAsync()
    {
        await _panel.DisposeAsync();
        await ((IAsyncLifetime)_app).DisposeAsync();
    }

    [Fact]
    public async Task ClickIsAcknowledgedAndSaved()
    {
        await _panel.ClickAsync("0.2", "LOW", seq: 17, ageMs: 5400);

        var ack = await _panel.ExpectAsync("ack");
        var product = await _app.FindProductAsync(_milk);

        Assert.Equal(17, ack.GetProperty("seq").GetInt64());
        Assert.Equal("0.2", ack.GetProperty("addr").GetString());
        Assert.True(ack.GetProperty("accepted").GetBoolean());
        Assert.Equal("LOW", ack.GetProperty("state").GetString());
        Assert.Equal(product!.Rev, ack.GetProperty("rev").GetInt64());
        Assert.Equal(ProductState.LOW, product.State);
        Assert.Equal($"device:{TestApp.DeviceId}", product.ChangedBy);
        Assert.Equal(_app.Clock.Now.AddMilliseconds(-5400), product.ChangedAt);
    }

    [Fact]
    public async Task RetryAfterLostAckDoesNotRaiseRev()
    {
        await _panel.ClickAsync("0.2", "LOW", seq: 5, ageMs: 100);
        var first = await _panel.ExpectAsync("ack");
        _app.Clock.Advance(TimeSpan.FromSeconds(5));

        await _panel.ClickAsync("0.2", "LOW", seq: 5, ageMs: 5100);
        var second = await _panel.ExpectAsync("ack");

        Assert.True(second.GetProperty("accepted").GetBoolean());
        Assert.Equal(first.GetProperty("rev").GetInt64(), second.GetProperty("rev").GetInt64());
    }

    [Fact]
    public async Task RetryOnNewConnectionDoesNotRaiseRev()
    {
        await _panel.ClickAsync("0.2", "LOW", seq: 5, ageMs: 100);
        var first = await _panel.ExpectAsync("ack");
        await _panel.DisposeAsync();
        _panel = await _app.ConnectIntroducedAsync();

        await _panel.ClickAsync("0.2", "LOW", seq: 5, ageMs: 3000);
        var second = await _panel.ExpectAsync("ack");

        Assert.Equal(first.GetProperty("rev").GetInt64(), second.GetProperty("rev").GetInt64());
    }

    [Fact]
    public async Task OlderOfflineClickLosesToWebChange()
    {
        _app.Clock.Advance(Step);
        await _app.ChangeFromWebAsync(_milk, ProductState.OUT);
        await _panel.ExpectAsync("state_set");
        _app.Clock.Advance(Step);

        await _panel.ClickAsync("0.2", "LOW", seq: 1, ageMs: (long)(Step * 1.5).TotalMilliseconds);
        var ack = await _panel.ExpectAsync("ack");

        Assert.False(ack.GetProperty("accepted").GetBoolean());
        Assert.Equal("OUT", ack.GetProperty("state").GetString());
    }

    [Fact]
    public async Task ClickAfterRestartLosesToChangeMadeMeanwhile()
    {
        var known = (await _app.FindProductAsync(_milk))!.Rev;
        await _app.ChangeFromWebAsync(_milk, ProductState.OUT);
        await _panel.ExpectAsync("state_set");

        await _panel.ClickAfterRestartAsync("0.2", "LOW", seq: 1, baseRev: known);
        var ack = await _panel.ExpectAsync("ack");

        Assert.False(ack.GetProperty("accepted").GetBoolean());
        Assert.Equal("OUT", ack.GetProperty("state").GetString());
    }

    [Fact]
    public async Task ClickAfterRestartIsAcceptedWhenNothingChanged()
    {
        var known = (await _app.FindProductAsync(_milk))!.Rev;

        await _panel.ClickAfterRestartAsync("0.2", "LOW", seq: 1, baseRev: known);
        var ack = await _panel.ExpectAsync("ack");

        Assert.True(ack.GetProperty("accepted").GetBoolean());
        Assert.Equal(_app.Clock.Now, (await _app.FindProductAsync(_milk))!.ChangedAt);
    }

    [Fact]
    public async Task ClickOfKeyWithoutProductGetsRejectionAndSnapshot()
    {
        await _panel.ClickAsync("0.3", "LOW", seq: 9, ageMs: 100);

        var ack = await _panel.ExpectAsync("ack");
        var snapshot = await _panel.ExpectAsync("snapshot");

        Assert.Equal(9, ack.GetProperty("seq").GetInt64());
        Assert.False(ack.GetProperty("accepted").GetBoolean());
        Assert.Equal("OK", ack.GetProperty("state").GetString());
        Assert.False(snapshot.GetProperty("keys")[3].GetProperty("assigned").GetBoolean());
    }

    [Fact]
    public async Task ClickOfUnknownKeyIsAlsoAcknowledged()
    {
        await _panel.ClickAsync("9.9", "LOW", seq: 3, ageMs: 100);

        Assert.False((await _panel.ExpectAsync("ack")).GetProperty("accepted").GetBoolean());
        await _panel.ExpectAsync("snapshot");
    }

    [Fact]
    public async Task UnknownStateIsRejectedSoPanelStopsRetrying()
    {
        await _panel.ClickAsync("0.2", "PURPLE", seq: 4, ageMs: 100);

        var ack = await _panel.ExpectAsync("ack");

        Assert.False(ack.GetProperty("accepted").GetBoolean());
        Assert.Equal("OK", ack.GetProperty("state").GetString());
    }

    [Fact]
    public async Task ChangeWithoutAgeAndBaseRevIsRejected()
    {
        await _panel.SendAsync("""{"v":1,"t":"state_change","seq":6,"addr":"0.2","state":"LOW"}""");

        Assert.False((await _panel.ExpectAsync("ack")).GetProperty("accepted").GetBoolean());
    }

    [Fact]
    public async Task WebChangeReachesPanelAsStateSet()
    {
        var result = await _app.ChangeFromWebAsync(_milk, ProductState.OUT);

        var message = await _panel.ExpectAsync("state_set");

        Assert.Equal("0.2", message.GetProperty("addr").GetString());
        Assert.Equal("OUT", message.GetProperty("state").GetString());
        Assert.Equal(result!.Rev, message.GetProperty("rev").GetInt64());
        Assert.Equal(TestApp.User, message.GetProperty("changed_by").GetString());
        Assert.Matches(new Regex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$"), message.GetProperty("changed_at").GetString()!);
    }

    [Fact]
    public async Task ChangeOfProductWithoutKeyDoesNotTouchPanel()
    {
        var eggs = await _app.AddProductAsync("Jajka");
        _app.Clock.Advance(Step);

        var result = await _app.ChangeFromWebAsync(eggs, ProductState.LOW);

        Assert.True(result!.Accepted);
        await _panel.ExpectSilenceAsync();
    }

    [Fact]
    public async Task ChangingManyProductsSendsOneSnapshot()
    {
        var eggs = await _app.AddProductAsync("Jajka");
        await _app.BindAsync(eggs, "0.0");
        await _panel.ExpectAsync("snapshot");
        _app.Clock.Advance(Step);

        await _app.UseProductsAsync(service => service.ChangeStatesAsync(
            [new(_milk, ProductState.LOW), new(eggs, ProductState.OUT)], TestApp.User, CancellationToken.None));

        var keys = (await _panel.ExpectAsync("snapshot")).GetProperty("keys");
        Assert.Equal("OUT", keys[0].GetProperty("state").GetString());
        Assert.Equal("LOW", keys[2].GetProperty("state").GetString());
        await _panel.ExpectSilenceAsync();
    }

    [Fact]
    public async Task OfflineWebChangeLosesToNewerPanelClick()
    {
        await _panel.ClickAsync("0.2", "LOW", seq: 1, ageMs: 0);
        await _panel.ExpectAsync("ack");
        _app.Clock.Advance(Step);

        var result = await _app.UseProductsAsync(service => service.ChangeStateAsync(
            new(_milk, ProductState.OK, AgeMs: (long)(Step * 2).TotalMilliseconds), TestApp.User, CancellationToken.None));

        Assert.False(result!.Accepted);
        Assert.Equal(ProductState.LOW, result.State);
    }

    [Fact]
    public async Task RepeatedWebChangeIsSafe()
    {
        var request = new Application.Products.ProductStateRequest(_milk, ProductState.OUT, ChangeKey: "web:outbox-1");

        var first = await _app.UseProductsAsync(service => service.ChangeStateAsync(request, TestApp.User, CancellationToken.None));
        await _panel.ExpectAsync("state_set");
        _app.Clock.Advance(Step);
        var second = await _app.UseProductsAsync(service => service.ChangeStateAsync(request, TestApp.User, CancellationToken.None));

        Assert.True(second!.Accepted);
        Assert.Equal(first!.Rev, second.Rev);
        await _panel.ExpectSilenceAsync();
    }
}
