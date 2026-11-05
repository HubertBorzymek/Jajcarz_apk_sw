using Jajcarz.Application.Products;
using Jajcarz.Domain.Products;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Jajcarz.Server.Api;

public sealed record ProductFieldsRequest(string? Name, string? Category);

public sealed record ProductStateChangeRequest(ProductState State, long AgeMs = 0, string? ChangeKey = null);

public sealed record ProductStateResponse(bool Accepted, ProductView Product);

public static class ProductEndpoints
{
    private const int MaxChangeKeyLength = 64;

    private static readonly long MaxAgeMs = (long)TimeSpan.FromDays(30).TotalMilliseconds;

    public static void MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").RequireAuthorization().AddEndpointFilter<AntiforgeryFilter>();
        group.MapGet("", List);
        group.MapPost("", Create);
        group.MapPut("/{id:guid}", Update);
        group.MapDelete("/{id:guid}", Delete);
        group.MapPost("/{id:guid}/state", ChangeState);
    }

    private static Task<IReadOnlyList<ProductView>> List(
        [FromServices] ProductService products, CancellationToken cancellationToken) =>
        products.ListAsync(cancellationToken);

    private static async Task<IResult> Create(
        ProductFieldsRequest request,
        HttpContext http,
        [FromServices] ProductService products,
        CancellationToken cancellationToken)
    {
        if (ProductFields.Normalize(request.Name, request.Category) is not { } fields)
        {
            return InvalidFields();
        }
        var product = await products.CreateAsync(fields.Name, fields.Category, http.User.Actor(), cancellationToken);
        return TypedResults.Created($"/api/products/{product.Id}", ProductView.From(product, null));
    }

    private static async Task<IResult> Update(
        Guid id, ProductFieldsRequest request, [FromServices] ProductService products, CancellationToken cancellationToken)
    {
        if (ProductFields.Normalize(request.Name, request.Category) is not { } fields)
        {
            return InvalidFields();
        }
        var product = await products.UpdateAsync(id, fields.Name, fields.Category, cancellationToken);
        return product is null ? TypedResults.NotFound() : TypedResults.Ok(product);
    }

    private static async Task<Results<NoContent, NotFound>> Delete(
        Guid id, [FromServices] ProductService products, CancellationToken cancellationToken) =>
        await products.DeleteAsync(id, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();

    private static async Task<IResult> ChangeState(
        Guid id,
        ProductStateChangeRequest request,
        HttpContext http,
        [FromServices] ProductService products,
        CancellationToken cancellationToken)
    {
        if (request.AgeMs < 0 || request.AgeMs > MaxAgeMs || request.ChangeKey?.Length > MaxChangeKeyLength)
        {
            return Results.Problem("Niepoprawna zmiana stanu.", statusCode: StatusCodes.Status400BadRequest);
        }

        // Prefiks autora: klucz z aplikacji nie może udawać klucza panelu ani innej osoby.
        var actor = http.User.Actor();
        var changeKey = request.ChangeKey is null ? null : $"{actor}:{request.ChangeKey}";
        var change = new ProductStateRequest(id, request.State, request.AgeMs, changeKey);
        var result = await products.ChangeStateAsync(change, actor, cancellationToken);
        var product = result is null ? null : await products.FindAsync(id, cancellationToken);
        return product is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new ProductStateResponse(result!.Accepted, product));
    }

    private static IResult InvalidFields() => Results.Problem(
        $"Nazwa jest wymagana i może mieć {Product.MaxNameLength} znaków, a kategoria {Product.MaxCategoryLength}.",
        statusCode: StatusCodes.Status400BadRequest);
}
