using Jajcarz.Application.Abstractions;
using Jajcarz.Domain.Panels;
using Jajcarz.Domain.Products;
using Microsoft.Extensions.Logging;

namespace Jajcarz.Application.Panels;

/// <summary>Obsługa wiadomości od panelu.</summary>
public sealed class PanelService(
    IStore store, PanelLinks links, SnapshotBuilder snapshots, TimeProvider clock, ILogger<PanelService> logger)
{
    public async Task<string?> AuthenticateAsync(string token, CancellationToken cancellationToken)
    {
        var device = await store.FindDeviceByTokenHashAsync(DeviceToken.Hash(token), cancellationToken);
        return device?.Id;
    }

    public async Task ConnectAsync(string deviceId, PanelHello hello, IPanelChannel channel, CancellationToken cancellationToken)
    {
        await using var write = await store.BeginWriteAsync(cancellationToken);
        var device = await RequireDeviceAsync(deviceId, cancellationToken);
        await UpdateKeysAsync(deviceId, hello.Segments, cancellationToken);
        device.ReportFirmware(hello.Fw);
        device.MarkSeen(clock.GetUtcNow());
        await write.CommitAsync(cancellationToken);

        links.Attach(deviceId, channel);
        links.Send(deviceId, await snapshots.BuildAsync(device, cancellationToken));
    }

    public async Task DisconnectAsync(string deviceId, IPanelChannel channel, CancellationToken cancellationToken)
    {
        await using var write = await store.BeginWriteAsync(cancellationToken);
        links.Detach(deviceId, channel);
        var device = await RequireDeviceAsync(deviceId, cancellationToken);
        device.MarkSeen(clock.GetUtcNow());
        await write.CommitAsync(cancellationToken);
    }

    public async Task ApplyStateChangeAsync(string deviceId, PanelStateChange message, CancellationToken cancellationToken)
    {
        await using var write = await store.BeginWriteAsync(cancellationToken);
        var device = await RequireDeviceAsync(deviceId, cancellationToken);
        device.MarkSeen(clock.GetUtcNow());
        var key = await store.FindKeyAsync(deviceId, message.Addr, cancellationToken);
        var product = key?.ProductId is { } productId ? await store.FindProductAsync(productId, cancellationToken) : null;

        if (key is null || product is null)
        {
            var rev = await store.CurrentRevAsync(cancellationToken);
            await write.CommitAsync(cancellationToken);
            links.Send(deviceId, new AckMessage(message.Seq, message.Addr, false, ProductState.OK, rev));
            links.Send(deviceId, await snapshots.BuildAsync(device, cancellationToken));
            return;
        }

        var outcome = await ApplyAsync(deviceId, message, key, product, cancellationToken);
        await write.CommitAsync(cancellationToken);
        links.Send(deviceId, new AckMessage(
            message.Seq, message.Addr, outcome != ChangeOutcome.Rejected, product.State, product.Rev));
    }

    public void ReportUnassignedPress(string deviceId, PanelUnassignedPress press) =>
        logger.LogInformation("Panel {DeviceId}: naciśnięto klawisz bez produktu {Addr}", deviceId, press.Addr);

    private async Task<ChangeOutcome> ApplyAsync(
        string deviceId, PanelStateChange message, PanelKey key, Product product, CancellationToken cancellationToken)
    {
        if (!ProductStates.TryParse(message.State, out var state))
        {
            logger.LogWarning("Panel {DeviceId}: nieznany stan {State}", deviceId, message.State);
            return ChangeOutcome.Rejected;
        }

        var change = new StateChange(
            state,
            $"device:{deviceId}",
            clock.GetUtcNow(),
            message.AgeMs,
            message.BaseRev,
            $"device:{deviceId}:{message.Seq}");
        var outcome = ConflictPolicy.Decide(product, key.BoundAt, change);
        if (outcome == ChangeOutcome.Accepted)
        {
            product.Apply(change, await store.NextRevAsync(cancellationToken));
        }
        return outcome;
    }

    private async Task UpdateKeysAsync(string deviceId, IReadOnlyList<PanelSegment> segments, CancellationToken cancellationToken)
    {
        var reported = segments
            .SelectMany(segment => Enumerable.Range(0, segment.Keys).Select(key => KeyAddress.Format(segment.Seg, key)))
            .ToHashSet();
        var known = await store.ListKeysAsync(deviceId, cancellationToken);
        foreach (var key in known)
        {
            key.MarkPresent(reported.Contains(key.Addr));
        }
        foreach (var addr in reported.Except(known.Select(key => key.Addr)))
        {
            store.Add(new PanelKey(deviceId, addr, present: true));
        }
    }

    private async Task<Device> RequireDeviceAsync(string deviceId, CancellationToken cancellationToken) =>
        await store.FindDeviceAsync(deviceId, cancellationToken)
        ?? throw new InvalidOperationException($"Brak panelu {deviceId}");
}
