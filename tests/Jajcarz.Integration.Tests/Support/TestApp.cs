using Jajcarz.Application.Abstractions;
using Jajcarz.Application.Panels;
using Jajcarz.Application.Products;
using Jajcarz.Domain.Panels;
using Jajcarz.Domain.Products;
using Jajcarz.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Jajcarz.Integration.Tests.Support;

public sealed class FakeClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan time) => Now += time;
}

/// <summary>
/// Serwer na tymczasowej bazie, z zarejestrowanym panelem i własnym zegarem. Domyślnie używa
/// SQLite, a gdy ustawiona jest zmienna JAJCARZ_TEST_POSTGRES, prawdziwego Postgresa.
/// </summary>
public sealed class TestApp : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DeviceId = "aabbccddeeff";
    public const string Token = "panel-test-token";
    public const string User = "web:hubert";

    private static readonly string? PostgresServer = Environment.GetEnvironmentVariable("JAJCARZ_TEST_POSTGRES");

    private readonly string _databaseName;
    private readonly bool _ownsDatabase;

    public TestApp()
    {
        _databaseName = $"jajcarz_test_{Guid.NewGuid():N}";
        _ownsDatabase = true;
    }

    private TestApp(string databaseName)
    {
        _databaseName = databaseName;
        _ownsDatabase = false;
    }

    public FakeClock Clock { get; } = new();

    private string SqlitePath => Path.Combine(Path.GetTempPath(), $"{_databaseName}.db");

    /// <summary>Drugi serwer na tej samej bazie, czyli to samo co restart aplikacji.</summary>
    public TestApp Restarted() => new(_databaseName);

    public async Task InitializeAsync()
    {
        if (!_ownsDatabase)
        {
            return;
        }
        await UseStoreAsync(async store =>
        {
            await using var write = await store.BeginWriteAsync(CancellationToken.None);
            store.Add(new Device(DeviceId, DeviceToken.Hash(Token)));
            await write.CommitAsync(CancellationToken.None);
        });
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_ownsDatabase)
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<JajcarzDbContext>().Database.EnsureDeletedAsync();
        }
        await DisposeAsync();
        SqliteConnection.ClearAllPools();
    }

    public async Task<FakePanel> ConnectAsync(string token = Token)
    {
        var client = Server.CreateWebSocketClient();
        client.ConfigureRequest = request => request.Headers.Authorization = $"Bearer {token}";
        return new FakePanel(await client.ConnectAsync(new Uri("ws://localhost/ws/device"), CancellationToken.None));
    }

    /// <summary>Panel po hello, z odebranym pierwszym snapshot.</summary>
    public async Task<FakePanel> ConnectIntroducedAsync()
    {
        var panel = await ConnectAsync();
        await panel.HelloAsync();
        await panel.ExpectAsync("snapshot");
        return panel;
    }

    public async Task<Guid> AddProductAsync(string name)
    {
        var product = await UseProductsAsync(service => service.CreateAsync(name, null, User, CancellationToken.None));
        return product.Id;
    }

    public Task<bool> BindAsync(Guid productId, string addr) =>
        UseProductsAsync(service => service.BindAsync(productId, DeviceId, addr, CancellationToken.None));

    public Task<ProductStateResult?> ChangeFromWebAsync(Guid productId, ProductState state) =>
        UseProductsAsync(service => service.ChangeStateAsync(new ProductStateRequest(productId, state), User, CancellationToken.None));

    public async Task<T> UseProductsAsync<T>(Func<ProductService, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ProductService>());
    }

    public async Task UseStoreAsync(Func<IStore, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<IStore>());
    }

    public async Task<T> UseStoreAsync<T>(Func<IStore, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IStore>());
    }

    public Task<Product?> FindProductAsync(Guid id) =>
        UseStoreAsync(store => store.FindProductAsync(id, CancellationToken.None));

    public Task<PanelKey?> FindKeyAsync(string addr) =>
        UseStoreAsync(store => store.FindKeyAsync(DeviceId, addr, CancellationToken.None));

    public Task<Device?> FindDeviceAsync() =>
        UseStoreAsync(store => store.FindDeviceAsync(DeviceId, CancellationToken.None));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        if (PostgresServer is null)
        {
            builder.UseSetting("Database:Provider", "Sqlite");
            builder.UseSetting("ConnectionStrings:Jajcarz", $"Data Source={SqlitePath}");
        }
        else
        {
            builder.UseSetting("Database:Provider", "Postgres");
            builder.UseSetting("ConnectionStrings:Jajcarz", $"{PostgresServer};Database={_databaseName}");
        }
        builder.ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
    }
}
