using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Jajcarz.Application.Products;
using Microsoft.AspNetCore.Mvc;

namespace Jajcarz.Server.Api;

/// <summary>Zmiany na żywo dla przeglądarki. Po każdym połączeniu klient dostaje najpierw całą listę.</summary>
public static class EventStreamEndpoint
{
    private const string ListEvent = "products";
    private const string UpdatedEvent = "product";
    private const string RemovedEvent = "removed";
    private const string PanelEvent = "panel";
    private const string KeyPressEvent = "key-press";
    private const string HeartbeatEvent = "ping";

    /// <summary>Krótszy niż limity bezczynności pośredników, a klient widzi, że połączenie żyje.</summary>
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(25);

    public static void MapEventStream(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/events", OpenStream).RequireAuthorization();

    private static IResult OpenStream(
        ProductEvents events,
        [FromServices] ProductService products,
        IHostApplicationLifetime lifetime,
        CancellationToken requestAborted) =>
        TypedResults.ServerSentEvents(StreamAsync(events, products, lifetime.ApplicationStopping, requestAborted));

    private static async IAsyncEnumerable<SseItem<object>> StreamAsync(
        ProductEvents events,
        ProductService products,
        CancellationToken stopping,
        [EnumeratorCancellation] CancellationToken requestAborted)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stopping, requestAborted);
        var cancellationToken = linked.Token;

        // Najpierw subskrypcja, potem lista: zmiana w międzyczasie przyjdzie po liście.
        using var subscription = events.Subscribe();
        yield return new SseItem<object>(await products.ListAsync(cancellationToken), ListEvent);

        while (await NextAsync(subscription, cancellationToken) is { } next)
        {
            yield return next;
        }
    }

    /// <summary>Null oznacza koniec strumienia: wyłączenie serwera albo odbiorcę, który nie nadążył.</summary>
    private static async Task<SseItem<object>?> NextAsync(ProductSubscription subscription, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(HeartbeatInterval);
        try
        {
            return await subscription.Reader.ReadAsync(timeout.Token) switch
            {
                ProductUpdated updated => new SseItem<object>(updated.Product, UpdatedEvent),
                ProductRemoved removed => new SseItem<object>(new { removed.ProductId }, RemovedEvent),
                PanelChanged panel => new SseItem<object>(new { panel.DeviceId }, PanelEvent),
                UnassignedKeyPressed press => new SseItem<object>(new { press.DeviceId, press.Addr }, KeyPressEvent),
                var other => throw new InvalidOperationException($"Nieznane zdarzenie {other.GetType()}"),
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SseItem<object>(new { }, HeartbeatEvent);
        }
        catch (Exception exception) when (exception is OperationCanceledException or ChannelClosedException)
        {
            return null;
        }
    }
}
