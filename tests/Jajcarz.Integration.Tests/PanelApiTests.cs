using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Jajcarz.Application.Panels;
using Jajcarz.Domain.Panels;
using Jajcarz.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Jajcarz.Integration.Tests;

public class PanelApiTests : IAsyncLifetime
{
    private const string Panels = "/api/panels";
    private const string Device = $"{Panels}/{TestApp.DeviceId}";

    private static readonly TimeSpan Step = TimeSpan.FromMinutes(1);

    private static readonly object ValidConfig = new
    {
        brightness = 30,
        colors = new Dictionary<string, int[]> { ["OK"] = [1, 2, 3], ["LOW"] = [4, 5, 6], ["OUT"] = [7, 8, 9] },
    };

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

    private async Task<JsonElement> PanelAsync()
    {
        var response = await _client.GetAsync(Panels);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var panels = await response.Content.ReadFromJsonAsync<JsonElement>();
        return panels.EnumerateArray().Single();
    }

    [Fact]
    public async Task ListRequiresLogin()
    {
        await using var anonymous = new ApiClient(_app);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Panels)).StatusCode);
    }

    [Fact]
    public async Task ListShowsConnectionFirmwareAndKeys()
    {
        var offline = await PanelAsync();
        Assert.False(offline.GetProperty("connected").GetBoolean());

        await using var panel = await _app.ConnectIntroducedAsync();
        var milk = await _app.AddProductAsync("Mleko");
        await _app.BindAsync(milk, "0.1");

        var online = await PanelAsync();
        Assert.True(online.GetProperty("connected").GetBoolean());
        Assert.Equal("v0.3.0", online.GetProperty("firmware").GetString());
        var keys = online.GetProperty("keys");
        Assert.Equal(4, keys.GetArrayLength());
        Assert.Equal(JsonValueKind.Null, keys[0].GetProperty("productId").ValueKind);
        Assert.Equal("Mleko", keys[1].GetProperty("productName").GetString());
        Assert.Equal("OK", keys[1].GetProperty("state").GetString());
    }

    [Fact]
    public async Task KeysOfRemovedModuleAreHidden()
    {
        await using (var first = await _app.ConnectAsync())
        {
            await first.HelloAsync(keys: 4);
            await first.ExpectAsync("snapshot");
        }

        await using var second = await _app.ConnectAsync();
        await second.HelloAsync(keys: 2);
        await second.ExpectAsync("snapshot");

        Assert.Equal(2, (await PanelAsync()).GetProperty("keys").GetArrayLength());
    }

    [Fact]
    public async Task BindAndUnbindThroughApiReachPanel()
    {
        await using var panel = await _app.ConnectIntroducedAsync();
        var milk = await _client.CreateProductAsync("Mleko");

        var bound = await _client.SendAsync(
            HttpMethod.Put, $"/api/products/{milk.Id}/key", new { deviceId = TestApp.DeviceId, addr = "0.2" });
        Assert.Equal(HttpStatusCode.NoContent, bound.StatusCode);
        var snapshot = await panel.ExpectAsync("snapshot");
        Assert.True(snapshot.GetProperty("keys")[2].GetProperty("assigned").GetBoolean());

        var unbound = await _client.SendAsync(HttpMethod.Delete, $"/api/products/{milk.Id}/key");
        Assert.Equal(HttpStatusCode.NoContent, unbound.StatusCode);
        snapshot = await panel.ExpectAsync("snapshot");
        Assert.False(snapshot.GetProperty("keys")[2].GetProperty("assigned").GetBoolean());
    }

    [Fact]
    public async Task BindingToUnknownKeyIsRejected()
    {
        var milk = await _client.CreateProductAsync("Mleko");

        var response = await _client.SendAsync(
            HttpMethod.Put, $"/api/products/{milk.Id}/key", new { deviceId = TestApp.DeviceId, addr = "9.9" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ConfigReachesPanelAndNextSnapshot()
    {
        await using (var panel = await _app.ConnectIntroducedAsync())
        {
            var response = await _client.SendAsync(HttpMethod.Put, $"{Device}/config", ValidConfig);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var message = await panel.ExpectAsync("config");
            Assert.Equal(30, message.GetProperty("brightness").GetInt32());
            Assert.Equal(3, message.GetProperty("colors").GetProperty("OK")[2].GetInt32());
        }

        await using var reconnected = await _app.ConnectAsync();
        await reconnected.HelloAsync();
        var snapshot = await reconnected.ExpectAsync("snapshot");
        Assert.Equal(30, snapshot.GetProperty("config").GetProperty("brightness").GetInt32());
    }

    [Fact]
    public async Task ConfigSurvivesRestart()
    {
        await _client.SendAsync(HttpMethod.Put, $"{Device}/config", ValidConfig);

        Assert.Equal(30, (await PanelAsync()).GetProperty("config").GetProperty("brightness").GetInt32());
        Assert.Equal(30, (await _app.FindDeviceAsync())!.Config.Brightness);
    }

    [Theory]
    [InlineData(256, 0)]
    [InlineData(-1, 0)]
    [InlineData(30, 300)]
    public async Task InvalidConfigIsRejected(int brightness, int channel)
    {
        var config = new
        {
            brightness,
            colors = new Dictionary<string, int[]> { ["OK"] = [channel, 0, 0], ["LOW"] = [0, 0, 0], ["OUT"] = [0, 0, 0] },
        };

        var response = await _client.SendAsync(HttpMethod.Put, $"{Device}/config", config);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfigWithoutAllStatesIsRejected()
    {
        var config = new { brightness = 30, colors = new Dictionary<string, int[]> { ["OK"] = [0, 0, 0] } };

        var response = await _client.SendAsync(HttpMethod.Put, $"{Device}/config", config);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangesWithoutCsrfTokenAreRejected()
    {
        var response = await _client.SendAsync(HttpMethod.Put, $"{Device}/config", ValidConfig, withCsrf: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task IdentifyReachesPanel()
    {
        await using var panel = await _app.ConnectIntroducedAsync();

        var response = await _client.SendAsync(HttpMethod.Post, $"{Device}/keys/0.2/identify");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var message = await panel.ExpectAsync("identify");
        Assert.Equal("0.2", message.GetProperty("addr").GetString());
        Assert.Equal(3000, message.GetProperty("duration_ms").GetInt32());
    }

    [Fact]
    public async Task IdentifyOnDisconnectedPanelReportsConflict()
    {
        await _app.UseStoreAsync(async store =>
        {
            await using var write = await store.BeginWriteAsync(CancellationToken.None);
            store.Add(new PanelKey(TestApp.DeviceId, "0.2", present: true));
            await write.CommitAsync(CancellationToken.None);
        });

        var response = await _client.SendAsync(HttpMethod.Post, $"{Device}/keys/0.2/identify");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task IdentifyOfUnknownKeyIsNotFound()
    {
        await using var panel = await _app.ConnectIntroducedAsync();

        var response = await _client.SendAsync(HttpMethod.Post, $"{Device}/keys/9.9/identify");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnassignedPressArrivesInAppAndUpdatesLastSeen()
    {
        await using var panel = await _app.ConnectIntroducedAsync();
        await using var events = await _client.OpenEventsAsync();
        await events.ExpectAsync("products");
        _app.Clock.Advance(Step);

        await panel.PressUnassignedAsync("0.3");

        var press = await events.ExpectAsync("key-press");
        Assert.Equal(TestApp.DeviceId, press.GetProperty("deviceId").GetString());
        Assert.Equal("0.3", press.GetProperty("addr").GetString());
        Assert.Equal(_app.Clock.Now, (await _app.FindDeviceAsync())!.LastSeenAt);
    }

    [Fact]
    public async Task ConnectingAndBindingSignalPanelChange()
    {
        await using var events = await _client.OpenEventsAsync();
        await events.ExpectAsync("products");

        await using var panel = await _app.ConnectIntroducedAsync();
        Assert.Equal(TestApp.DeviceId, (await events.ExpectAsync("panel")).GetProperty("deviceId").GetString());

        var milk = await _client.CreateProductAsync("Mleko");
        await events.ExpectAsync("product");
        await _app.BindAsync(milk.Id, "0.0");
        await events.ExpectAsync("product");
        await events.ExpectAsync("panel");
    }

    [Fact]
    public async Task SetTokenRegistersPanelAndReplacesToken()
    {
        const string newDevice = "112233445566";
        await using var scope = _app.Services.CreateAsyncScope();
        var panels = scope.ServiceProvider.GetRequiredService<PanelAdminService>();

        var first = await panels.SetTokenAsync(newDevice, CancellationToken.None);
        await using (var panel = await _app.ConnectAsync(first))
        {
            await panel.HelloAsync(newDevice);
            await panel.ExpectAsync("snapshot");
        }

        var second = await panels.SetTokenAsync(newDevice, CancellationToken.None);

        Assert.NotEqual(first, second);
        await Assert.ThrowsAnyAsync<Exception>(() => _app.ConnectAsync(first));
    }

    [Theory]
    [InlineData("za-krotki")]
    [InlineData("AABBCCDDEEFF")]
    [InlineData("aabbccddeeff00")]
    public async Task SetTokenRejectsInvalidDeviceId(string deviceId)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        var panels = scope.ServiceProvider.GetRequiredService<PanelAdminService>();

        await Assert.ThrowsAsync<ArgumentException>(() => panels.SetTokenAsync(deviceId, CancellationToken.None));
    }
}
