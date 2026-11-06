using System.Net;
using Jajcarz.Integration.Tests.Support;

namespace Jajcarz.Integration.Tests;

public class HealthTests : IAsyncLifetime
{
    private readonly TestApp _app = new();

    public Task InitializeAsync() => _app.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_app).DisposeAsync();

    [Fact]
    public async Task HealthDoesNotRequireLogin()
    {
        await using var anonymous = new ApiClient(_app);

        var response = await anonymous.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("OK", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownApiPathIsNotServedAsApplication()
    {
        await using var anonymous = new ApiClient(_app);

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/api/nie-ma")).StatusCode);
    }
}
