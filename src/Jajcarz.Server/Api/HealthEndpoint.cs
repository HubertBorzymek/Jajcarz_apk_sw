using Jajcarz.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace Jajcarz.Server.Api;

/// <summary>Stan aplikacji dla Dockera. Sprawdza też bazę, bo bez niej aplikacja nie działa.</summary>
public static class HealthEndpoint
{
    public static void MapHealth(this IEndpointRouteBuilder app) => app.MapGet("/healthz", CheckAsync);

    private static async Task<IResult> CheckAsync(
        [FromServices] IStore store, ILoggerFactory loggers, CancellationToken cancellationToken)
    {
        try
        {
            await store.CurrentRevAsync(cancellationToken);
            return Results.Text("OK");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            loggers.CreateLogger(nameof(HealthEndpoint)).LogError(exception, "Baza danych niedostępna");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }
}
