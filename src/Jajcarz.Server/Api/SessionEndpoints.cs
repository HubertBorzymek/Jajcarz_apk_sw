using System.Security.Claims;
using Jajcarz.Application.Accounts;
using Jajcarz.Domain.Accounts;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Jajcarz.Server.Api;

public sealed record LoginRequest(string? Login, string? Password);

public sealed record SessionResponse(string? Login, string CsrfToken);

public static class SessionEndpoints
{
    public const string LoginRateLimit = "login";

    private const int MaxPasswordLength = 256;

    public static void MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/session");
        group.MapGet("", GetSession);
        group.MapPost("", LogIn).AddEndpointFilter<AntiforgeryFilter>().RequireRateLimiting(LoginRateLimit);
        group.MapDelete("", (Delegate)LogOut).AddEndpointFilter<AntiforgeryFilter>().RequireAuthorization();
    }

    /// <summary>Publiczne. Token CSRF zależy od konta, więc klient pobiera go znowu po zalogowaniu.</summary>
    private static SessionResponse GetSession(HttpContext http, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(http);
        var login = http.User.Identity?.IsAuthenticated == true ? http.User.Identity.Name : null;
        return new SessionResponse(login, tokens.RequestToken!);
    }

    private static async Task<IResult> LogIn(
        LoginRequest request, HttpContext http, AccountService accounts, CancellationToken cancellationToken)
    {
        if (request.Login is not { Length: > 0 and <= User.MaxLoginLength } login
            || request.Password is not { Length: > 0 and <= MaxPasswordLength } password)
        {
            return Results.Unauthorized();
        }
        var user = await accounts.SignInAsync(login, password, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Login),
            new(ApiUser.SessionStampClaim, user.SessionStamp.ToString()),
        ];
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await http.SignInAsync(new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true });
        return Results.NoContent();
    }

    private static async Task<IResult> LogOut(HttpContext http)
    {
        await http.SignOutAsync();
        return Results.NoContent();
    }
}
