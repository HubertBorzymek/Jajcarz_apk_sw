using Jajcarz.Application.Products;
using Jajcarz.Domain.Products;
using Jajcarz.Integration.Tests.Support;

namespace Jajcarz.Integration.Tests;

public class EventStreamTests : IAsyncLifetime
{
    private static readonly TimeSpan Step = TimeSpan.FromMinutes(1);

    private readonly TestApp _app = new();
    private ApiClient _client = null!;

    public async Task InitializeAsync()
    {
        await _app.InitializeAsync();
        _client = await ApiClient.LoggedInAsync(_app);
    }

    public async Task DisposeAsync()
    {
        await _client.DisposeAsync();
        await ((IAsyncLifetime)_app).DisposeAsync();
    }

    [Fact]
    public async Task ListArrivesFirst()
    {
        var product = await _client.CreateProductAsync("Mleko");

        await using var events = await _client.OpenEventsAsync();

        var list = await events.ExpectAsync("products");
        Assert.Equal(product.Id, Assert.Single(list.EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ChangeFromPhoneArrivesOnOtherDevice()
    {
        var product = await _client.CreateProductAsync("Mleko");
        await using var other = await ApiClient.LoggedInAsync(_app);
        await using var events = await other.OpenEventsAsync();
        await events.ExpectAsync("products");
        _app.Clock.Advance(Step);

        await _client.ChangeStateAsync(product.Id, "OUT");

        var update = await events.ExpectAsync("product");
        Assert.Equal(product.Id, update.GetProperty("id").GetGuid());
        Assert.Equal("OUT", update.GetProperty("state").GetString());
        Assert.Equal(TestApp.User, update.GetProperty("changedBy").GetString());
    }

    [Fact]
    public async Task ClickOnPanelArrivesInApp()
    {
        await using var panel = await _app.ConnectIntroducedAsync();
        var product = await _client.CreateProductAsync("Mleko");
        await _app.BindAsync(product.Id, "0.2");
        await panel.ExpectAsync("snapshot");
        await using var events = await _client.OpenEventsAsync();
        await events.ExpectAsync("products");
        _app.Clock.Advance(Step);

        await panel.ClickAsync("0.2", "LOW", seq: 1, ageMs: 100);

        var update = await events.ExpectAsync("product");
        Assert.Equal("LOW", update.GetProperty("state").GetString());
        Assert.Equal("0.2", update.GetProperty("addr").GetString());
        Assert.Equal($"device:{TestApp.DeviceId}", update.GetProperty("changedBy").GetString());
    }

    [Fact]
    public async Task RejectedPanelChangeIsNotPublished()
    {
        await using var panel = await _app.ConnectIntroducedAsync();
        var product = await _client.CreateProductAsync("Mleko");
        await _app.BindAsync(product.Id, "0.2");
        await panel.ExpectAsync("snapshot");
        _app.Clock.Advance(Step);
        await _client.ChangeStateAsync(product.Id, "OUT");
        await panel.ExpectAsync("state_set");
        await using var events = await _client.OpenEventsAsync();
        await events.ExpectAsync("products");

        await panel.ClickAsync("0.2", "LOW", seq: 1, ageMs: (long)(Step * 2).TotalMilliseconds);
        await panel.ExpectAsync("ack");
        _app.Clock.Advance(Step);
        await _client.ChangeStateAsync(product.Id, "OK");

        var update = await events.ExpectAsync("product");
        Assert.Equal("OK", update.GetProperty("state").GetString());
    }

    [Fact]
    public async Task EditAndRemovalArrive()
    {
        var product = await _client.CreateProductAsync("Mleko");
        await using var events = await _client.OpenEventsAsync();
        await events.ExpectAsync("products");

        await _client.SendAsync(HttpMethod.Put, $"/api/products/{product.Id}", new { name = "Mleko 2%" });
        await _client.SendAsync(HttpMethod.Delete, $"/api/products/{product.Id}");

        var edit = await events.ExpectAsync("product");
        var removal = await events.ExpectAsync("removed");
        Assert.Equal("Mleko 2%", edit.GetProperty("name").GetString());
        Assert.Equal(product.Id, removal.GetProperty("productId").GetGuid());
    }

    [Fact]
    public async Task BindingShowsKeyInApp()
    {
        var product = await _client.CreateProductAsync("Mleko");
        await using var panel = await _app.ConnectIntroducedAsync();
        await using var events = await _client.OpenEventsAsync();
        await events.ExpectAsync("products");

        await _app.BindAsync(product.Id, "0.3");
        await _app.UseProductsAsync(service => service.UnbindAsync(product.Id, CancellationToken.None));

        Assert.Equal("0.3", (await events.ExpectAsync("product")).GetProperty("addr").GetString());
        await events.ExpectAsync("panel");
        Assert.Null((await events.ExpectAsync("product")).GetProperty("addr").GetString());
    }

    [Fact]
    public void SlowSubscriberIsDisconnectedInsteadOfBlockingWrites()
    {
        var events = new ProductEvents();
        var view = new ProductView(Guid.NewGuid(), "Mleko", null, ProductState.OK, 1, "x", DateTimeOffset.UtcNow, null);
        using var subscription = events.Subscribe();

        for (var i = 0; i < 300; i++)
        {
            events.Publish(new ProductUpdated(view));
        }

        while (subscription.Reader.TryRead(out _))
        {
        }
        Assert.True(subscription.Reader.Completion.IsCompleted);
    }
}
