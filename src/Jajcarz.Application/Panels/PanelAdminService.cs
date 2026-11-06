using System.Text.RegularExpressions;
using Jajcarz.Application.Abstractions;
using Jajcarz.Application.Products;
using Jajcarz.Domain.Panels;
using Jajcarz.Domain.Products;

namespace Jajcarz.Application.Panels;

public sealed record PanelKeyView(string Addr, Guid? ProductId, string? ProductName, ProductState? State);

/// <summary>Panel widziany w aplikacji. Klawisze odpiętych modułów zostają w bazie, ale tu się nie pojawiają.</summary>
public sealed record PanelView(
    string Id,
    string? Firmware,
    bool Connected,
    DateTimeOffset? LastSeenAt,
    PanelConfig Config,
    IReadOnlyList<PanelKeyView> Keys);

public enum IdentifyResult
{
    Sent,
    KeyNotFound,
    PanelOffline,
}

/// <summary>Zarządzanie panelami z aplikacji i z linii poleceń.</summary>
public sealed partial class PanelAdminService(IStore store, PanelLinks links, ProductEvents events)
{
    private const int IdentifyDurationMs = 3000;

    public async Task<IReadOnlyList<PanelView>> ListAsync(CancellationToken cancellationToken)
    {
        var devices = await store.ListDevicesAsync(cancellationToken);
        var products = (await store.ListProductsAsync(cancellationToken)).ToDictionary(product => product.Id);
        var panels = new List<PanelView>();
        foreach (var device in devices)
        {
            var keys = await store.ListKeysAsync(device.Id, cancellationToken);
            panels.Add(new PanelView(
                device.Id,
                device.Firmware,
                links.IsConnected(device.Id),
                device.LastSeenAt,
                device.Config,
                keys.Where(key => key.Present)
                    .OrderBy(key => KeyAddress.Order(key.Addr))
                    .Select(key => ToView(key, products))
                    .ToList()));
        }
        return panels;
    }

    /// <summary>Zapisuje wygląd panelu. Rozłączony panel dostanie go w snapshot przy połączeniu.</summary>
    public async Task<bool> ConfigureAsync(string deviceId, PanelConfig config, CancellationToken cancellationToken)
    {
        await using var write = await store.BeginWriteAsync(cancellationToken);
        var device = await store.FindDeviceAsync(deviceId, cancellationToken);
        if (device is null)
        {
            return false;
        }
        device.Configure(config);
        await write.CommitAsync(cancellationToken);
        links.Send(deviceId, ConfigMessage.For(config));
        events.Publish(new PanelChanged(deviceId));
        return true;
    }

    public async Task<IdentifyResult> IdentifyAsync(string deviceId, string addr, CancellationToken cancellationToken)
    {
        var key = await store.FindKeyAsync(deviceId, addr, cancellationToken);
        if (key is not { Present: true })
        {
            return IdentifyResult.KeyNotFound;
        }
        if (!links.IsConnected(deviceId))
        {
            return IdentifyResult.PanelOffline;
        }
        links.Send(deviceId, new IdentifyMessage(addr, IdentifyDurationMs));
        return IdentifyResult.Sent;
    }

    /// <summary>Rejestruje panel albo wymienia jego token. Zwraca nowy token, a baza zna tylko jego skrót.</summary>
    public async Task<string> SetTokenAsync(string deviceId, CancellationToken cancellationToken)
    {
        if (!DeviceIdFormat().IsMatch(deviceId))
        {
            throw new ArgumentException("device_id to 12 małych znaków hex, np. 6825dd21a970.", nameof(deviceId));
        }

        var token = DeviceToken.Create();
        var tokenHash = DeviceToken.Hash(token);
        await using var write = await store.BeginWriteAsync(cancellationToken);
        var device = await store.FindDeviceAsync(deviceId, cancellationToken);
        if (device is null)
        {
            store.Add(new Device(deviceId, tokenHash));
        }
        else
        {
            device.ReplaceToken(tokenHash);
        }
        await write.CommitAsync(cancellationToken);
        return token;
    }

    private static PanelKeyView ToView(PanelKey key, Dictionary<Guid, Product> products) =>
        key.ProductId is { } id && products.TryGetValue(id, out var product)
            ? new PanelKeyView(key.Addr, id, product.Name, product.State)
            : new PanelKeyView(key.Addr, null, null, null);

    [GeneratedRegex("^[0-9a-f]{12}$")]
    private static partial Regex DeviceIdFormat();
}
