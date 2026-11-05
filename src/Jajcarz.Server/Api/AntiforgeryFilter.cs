using Microsoft.AspNetCore.Antiforgery;

namespace Jajcarz.Server.Api;

/// <summary>
/// Cookie logowania idzie z każdym żądaniem do tej domeny, także wywołanym przez obcą stronę.
/// Token z nagłówka dowodzi, że zmianę wysłała nasza aplikacja.
/// </summary>
public sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (!HttpMethods.IsGet(http.Request.Method) && !await antiforgery.IsRequestValidAsync(http))
        {
            return Results.Problem("Nieważny token formularza.", statusCode: StatusCodes.Status403Forbidden);
        }
        return await next(context);
    }
}
