using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Jajcarz.Application.Products;

public abstract record ProductEvent;

public sealed record ProductUpdated(ProductView Product) : ProductEvent;

public sealed record ProductRemoved(Guid ProductId) : ProductEvent;

/// <summary>
/// Zmiany produktów dla otwartych aplikacji. Publish nie czeka na odbiorców, więc można go
/// wołać w trakcie zapisu. Dzięki temu zdarzenia mają tę samą kolejność co zmiany w bazie.
/// </summary>
public sealed class ProductEvents
{
    private const int SubscriberCapacity = 256;

    private readonly ConcurrentDictionary<ProductSubscription, byte> _subscriptions = new();

    public ProductSubscription Subscribe()
    {
        var subscription = new ProductSubscription(this, SubscriberCapacity);
        _subscriptions[subscription] = 0;
        return subscription;
    }

    public void Publish(ProductEvent productEvent)
    {
        foreach (var subscription in _subscriptions.Keys)
        {
            subscription.Offer(productEvent);
        }
    }

    internal void Remove(ProductSubscription subscription) => _subscriptions.TryRemove(subscription, out _);
}

/// <summary>Odbiorca, który nie nadąża, zostaje rozłączony i po ponownym połączeniu dostaje całą listę.</summary>
public sealed class ProductSubscription : IDisposable
{
    private readonly ProductEvents _events;
    private readonly Channel<ProductEvent> _channel;

    internal ProductSubscription(ProductEvents events, int capacity)
    {
        _events = events;
        _channel = Channel.CreateBounded<ProductEvent>(new BoundedChannelOptions(capacity) { SingleReader = true });
    }

    public ChannelReader<ProductEvent> Reader => _channel.Reader;

    public void Dispose() => _events.Remove(this);

    internal void Offer(ProductEvent productEvent)
    {
        if (!_channel.Writer.TryWrite(productEvent))
        {
            _channel.Writer.TryComplete();
            Dispose();
        }
    }
}
