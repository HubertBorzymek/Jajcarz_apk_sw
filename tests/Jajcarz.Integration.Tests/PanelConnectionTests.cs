using System.Net;
using System.Net.WebSockets;
using Jajcarz.Application.Panels;
using Jajcarz.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Jajcarz.Integration.Tests;

public class PanelConnectionTests : IAsyncLifetime
{
    private readonly TestApp _app = new();

    public Task InitializeAsync() => _app.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_app).DisposeAsync();

    [Fact]
    public async Task PlainHttpRequestIsRefused()
    {
        var response = await _app.CreateClient().GetAsync("/ws/device");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WrongTokenIsRefused()
    {
        var error = await Assert.ThrowsAnyAsync<Exception>(() => _app.ConnectAsync("wrong-token"));

        Assert.Contains("401", error.Message);
    }

    [Fact]
    public async Task HelloReturnsSnapshotInProtocolFormat()
    {
        var product = await _app.AddProductAsync("Mleko");
        await using var panel = await _app.ConnectAsync();

        await panel.HelloAsync();
        var snapshot = await panel.ExpectAsync("snapshot");
        await _app.BindAsync(product, "0.1");
        var bound = await panel.ExpectAsync("snapshot");

        Assert.Equal(1, snapshot.GetProperty("v").GetInt32());
        var keys = snapshot.GetProperty("keys");
        Assert.Equal(4, keys.GetArrayLength());
        Assert.Equal("0.0", keys[0].GetProperty("addr").GetString());
        Assert.False(keys[0].GetProperty("assigned").GetBoolean());
        Assert.False(keys[0].TryGetProperty("state", out _));
        var config = snapshot.GetProperty("config");
        Assert.Equal(51, config.GetProperty("brightness").GetInt32());
        Assert.Equal(120, config.GetProperty("colors").GetProperty("LOW")[1].GetInt32());

        var key = bound.GetProperty("keys")[1];
        Assert.True(key.GetProperty("assigned").GetBoolean());
        Assert.Equal("OK", key.GetProperty("state").GetString());
        Assert.True(key.GetProperty("rev").GetInt64() > 0);
    }

    [Fact]
    public async Task HelloStoresFirmwareAndLastContact()
    {
        await using var panel = await _app.ConnectIntroducedAsync();

        var device = await _app.FindDeviceAsync();

        Assert.Equal("v0.3.0", device!.Firmware);
        Assert.Equal(_app.Clock.Now, device.LastSeenAt);
    }

    [Fact]
    public async Task HelloWithOtherDeviceIdClosesConnection()
    {
        await using var panel = await _app.ConnectAsync();

        await panel.HelloAsync("ffffffffffff");

        Assert.Equal(WebSocketCloseStatus.PolicyViolation, await panel.ExpectClosedAsync());
    }

    [Fact]
    public async Task MessageBeforeHelloIsIgnored()
    {
        await using var panel = await _app.ConnectAsync();

        await panel.ClickAsync("0.0", "LOW", seq: 1, ageMs: 0);

        await panel.ExpectSilenceAsync();
    }

    [Fact]
    public async Task BrokenJsonKeepsConnectionOpen()
    {
        await using var panel = await _app.ConnectIntroducedAsync();

        await panel.SendAsync("{nie json");
        await panel.SendAsync("""{"v":1,"t":"state_change"}""");
        await panel.HelloAsync();

        await panel.ExpectAsync("snapshot");
    }

    [Fact]
    public async Task SnapshotKeysAreSortedByNumbers()
    {
        await using var panel = await _app.ConnectAsync();

        await panel.HelloAsync(keys: 11);
        var keys = (await panel.ExpectAsync("snapshot")).GetProperty("keys");

        Assert.Equal("0.2", keys[2].GetProperty("addr").GetString());
        Assert.Equal("0.10", keys[10].GetProperty("addr").GetString());
    }

    [Fact]
    public async Task RemovedModuleKeepsBindingAndLeavesSnapshot()
    {
        await using var panel = await _app.ConnectAsync();
        await panel.HelloWithSegmentsAsync("""[{"seg":0,"keys":2},{"seg":1,"keys":2}]""");
        await panel.ExpectAsync("snapshot");
        var product = await _app.AddProductAsync("Mleko");
        await _app.BindAsync(product, "1.0");
        await panel.ExpectAsync("snapshot");

        await panel.HelloWithSegmentsAsync("""[{"seg":0,"keys":2}]""");
        var withoutModule = await panel.ExpectAsync("snapshot");
        await panel.HelloWithSegmentsAsync("""[{"seg":0,"keys":2},{"seg":1,"keys":2}]""");
        var withModule = await panel.ExpectAsync("snapshot");

        Assert.Equal(2, withoutModule.GetProperty("keys").GetArrayLength());
        Assert.True(withModule.GetProperty("keys")[2].GetProperty("assigned").GetBoolean());
    }

    [Fact]
    public async Task NewConnectionReplacesOldOne()
    {
        await using var first = await _app.ConnectIntroducedAsync();
        await using var second = await _app.ConnectIntroducedAsync();
        var product = await _app.AddProductAsync("Mleko");

        await _app.BindAsync(product, "0.0");

        await second.ExpectAsync("snapshot");
    }

    [Fact]
    public async Task ClosingOldConnectionDoesNotDisconnectNewOne()
    {
        var first = await _app.ConnectIntroducedAsync();
        await using var second = await _app.ConnectIntroducedAsync();

        await first.DisposeAsync();
        var product = await _app.AddProductAsync("Mleko");
        await _app.BindAsync(product, "0.0");

        await second.ExpectAsync("snapshot");
        Assert.True(_app.Services.GetRequiredService<PanelLinks>().IsConnected(TestApp.DeviceId));
    }

    [Fact]
    public async Task UnassignedPressGetsNoResponse()
    {
        await using var panel = await _app.ConnectIntroducedAsync();

        await panel.PressUnassignedAsync("0.3");

        await panel.ExpectSilenceAsync();
    }
}
