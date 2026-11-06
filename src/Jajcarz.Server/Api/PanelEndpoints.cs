using Jajcarz.Application.Panels;
using Jajcarz.Domain.Panels;
using Jajcarz.Domain.Products;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Jajcarz.Server.Api;

public sealed record PanelConfigRequest(int Brightness, Dictionary<ProductState, int[]> Colors);

public static class PanelEndpoints
{
    public static void MapPanelEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/panels").RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>();
        group.MapGet("", List);
        group.MapPut("/{id}/config", Configure);
        group.MapPost("/{id}/keys/{addr}/identify", Identify);
    }

    private static Task<IReadOnlyList<PanelView>> List(
        [FromServices] PanelAdminService panels, CancellationToken cancellationToken) =>
        panels.ListAsync(cancellationToken);

    private static async Task<IResult> Configure(
        string id, PanelConfigRequest request, [FromServices] PanelAdminService panels, CancellationToken cancellationToken)
    {
        var config = new PanelConfig(request.Brightness, request.Colors);
        if (!config.IsValid())
        {
            return Results.Problem("Niepoprawna jasność lub kolory.", statusCode: StatusCodes.Status400BadRequest);
        }
        return await panels.ConfigureAsync(id, config, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static async Task<Results<NoContent, NotFound, ProblemHttpResult>> Identify(
        string id, string addr, [FromServices] PanelAdminService panels, CancellationToken cancellationToken) =>
        await panels.IdentifyAsync(id, addr, cancellationToken) switch
        {
            IdentifyResult.Sent => TypedResults.NoContent(),
            IdentifyResult.PanelOffline => TypedResults.Problem(
                "Panel jest rozłączony.", statusCode: StatusCodes.Status409Conflict),
            _ => TypedResults.NotFound(),
        };
}
