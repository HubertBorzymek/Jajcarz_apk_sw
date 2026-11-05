using Jajcarz.Application.Abstractions;
using Jajcarz.Application.Panels;
using Jajcarz.Application.Products;
using Jajcarz.Domain.Panels;

namespace Jajcarz.Server.Hosting;

/// <summary>Dane do pracy lokalnej z prawdziwym panelem. Uruchamiane tylko w środowisku Development.</summary>
public static class DevelopmentSeed
{
    private const string SectionName = "DevSeed";
    private const string Actor = "server";

    private sealed record SeedProduct(string Name, string Addr);

    private sealed record SeedOptions(string DeviceId, string Token, SeedProduct[] Products);

    public static async Task ApplyAsync(IServiceProvider services, IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<SeedOptions>();
        if (options is null)
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IStore>();
        if (await store.FindDeviceAsync(options.DeviceId, CancellationToken.None) is not null)
        {
            return;
        }

        await using (var write = await store.BeginWriteAsync(CancellationToken.None))
        {
            store.Add(new Device(options.DeviceId, DeviceToken.Hash(options.Token)));
            foreach (var product in options.Products)
            {
                store.Add(new PanelKey(options.DeviceId, product.Addr, present: false));
            }
            await write.CommitAsync(CancellationToken.None);
        }

        var products = scope.ServiceProvider.GetRequiredService<ProductService>();
        foreach (var seed in options.Products)
        {
            var product = await products.CreateAsync(seed.Name, null, Actor, CancellationToken.None);
            await products.BindAsync(product.Id, options.DeviceId, seed.Addr, CancellationToken.None);
        }
    }
}
