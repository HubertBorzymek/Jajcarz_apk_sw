using System.Net;
using System.Net.Http.Json;
using Jajcarz.Application.Products;
using Jajcarz.Domain.Products;
using Jajcarz.Integration.Tests.Support;

namespace Jajcarz.Integration.Tests;

public class ProductApiTests : IAsyncLifetime
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
    public async Task CreatedProductIsOnTheList()
    {
        var created = await _client.CreateProductAsync("  Mleko ", " Nabiał ");

        var product = Assert.Single(await _client.ListAsync());
        Assert.Equal(created.Id, product.Id);
        Assert.Equal("Mleko", product.Name);
        Assert.Equal("Nabiał", product.Category);
        Assert.Equal(ProductState.OK, product.State);
        Assert.Equal(TestApp.User, product.ChangedBy);
        Assert.Null(product.Addr);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyNameIsRejected(string name)
    {
        var response = await _client.SendAsync(HttpMethod.Post, "/api/products", new { name });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await _client.ListAsync());
    }

    [Fact]
    public async Task TooLongNameIsRejected()
    {
        var response = await _client.SendAsync(
            HttpMethod.Post, "/api/products", new { name = new string('x', Product.MaxNameLength + 1) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EditChangesFieldsButNotState()
    {
        var created = await _client.CreateProductAsync("Mleko");
        _app.Clock.Advance(Step);
        await _client.ChangeStateAsync(created.Id, "LOW");
        var before = await _app.FindProductAsync(created.Id);

        var response = await _client.SendAsync(
            HttpMethod.Put, $"/api/products/{created.Id}", new { name = "Mleko 2%", category = "Nabiał" });

        var edited = await response.Content.ReadFromJsonAsync<ProductView>(ApiClient.Json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Mleko 2%", edited!.Name);
        Assert.Equal("Nabiał", edited.Category);
        Assert.Equal(ProductState.LOW, edited.State);
        Assert.Equal(before!.Rev, edited.Rev);
    }

    [Fact]
    public async Task DeletedProductDisappears()
    {
        var created = await _client.CreateProductAsync("Mleko");

        var response = await _client.SendAsync(HttpMethod.Delete, $"/api/products/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await _client.ListAsync());
    }

    [Fact]
    public async Task UnknownProductIsNotFound()
    {
        var id = Guid.NewGuid();

        var state = await _client.SendAsync(HttpMethod.Post, $"/api/products/{id}/state", new { state = "OUT" });
        var edit = await _client.SendAsync(HttpMethod.Put, $"/api/products/{id}", new { name = "X" });
        var delete = await _client.SendAsync(HttpMethod.Delete, $"/api/products/{id}");

        Assert.Equal(HttpStatusCode.NotFound, state.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task StateChangeKeepsAuthorAndTime()
    {
        var created = await _client.CreateProductAsync("Mleko");
        _app.Clock.Advance(Step);

        var result = await _client.ChangeStateAsync(created.Id, "OUT");

        Assert.True(result.Accepted);
        Assert.Equal(ProductState.OUT, result.Product.State);
        Assert.Equal(TestApp.User, result.Product.ChangedBy);
        Assert.Equal(_app.Clock.Now, result.Product.ChangedAt);
    }

    [Fact]
    public async Task StateChangeReachesPanelAndKeyIsVisible()
    {
        await using var panel = await _app.ConnectIntroducedAsync();
        var created = await _client.CreateProductAsync("Mleko");
        await _app.BindAsync(created.Id, "0.1");
        await panel.ExpectAsync("snapshot");
        _app.Clock.Advance(Step);

        var result = await _client.ChangeStateAsync(created.Id, "LOW");

        var message = await panel.ExpectAsync("state_set");
        Assert.Equal("0.1", message.GetProperty("addr").GetString());
        Assert.Equal("LOW", message.GetProperty("state").GetString());
        Assert.Equal(result.Product.Rev, message.GetProperty("rev").GetInt64());
        Assert.Equal("0.1", result.Product.Addr);
    }

    [Fact]
    public async Task RetrySendingSameChangeDoesNotRaiseRev()
    {
        var created = await _client.CreateProductAsync("Mleko");
        _app.Clock.Advance(Step);
        var first = await _client.ChangeStateAsync(created.Id, "LOW", changeKey: "zmiana-1");
        _app.Clock.Advance(Step);

        var retry = await _client.ChangeStateAsync(created.Id, "LOW", changeKey: "zmiana-1");

        Assert.True(retry.Accepted);
        Assert.Equal(first.Product.Rev, retry.Product.Rev);
        Assert.Equal(first.Product.ChangedAt, retry.Product.ChangedAt);
    }

    [Fact]
    public async Task OlderQueuedChangeLosesToNewerOne()
    {
        var created = await _client.CreateProductAsync("Mleko");
        _app.Clock.Advance(Step);
        await _client.ChangeStateAsync(created.Id, "OUT");
        _app.Clock.Advance(Step);

        var queued = await _client.ChangeStateAsync(created.Id, "LOW", ageMs: (long)(Step * 1.5).TotalMilliseconds);

        Assert.False(queued.Accepted);
        Assert.Equal(ProductState.OUT, queued.Product.State);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(long.MaxValue)]
    public async Task ImpossibleAgeIsRejected(long ageMs)
    {
        var created = await _client.CreateProductAsync("Mleko");

        var response = await _client.SendAsync(
            HttpMethod.Post, $"/api/products/{created.Id}/state", new { state = "LOW", ageMs });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnknownStateIsRejected()
    {
        var created = await _client.CreateProductAsync("Mleko");

        var response = await _client.SendAsync(
            HttpMethod.Post, $"/api/products/{created.Id}/state", new { state = "HIGH" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
