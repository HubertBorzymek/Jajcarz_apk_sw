using System.Threading.RateLimiting;
using Jajcarz.Application.Accounts;
using Jajcarz.Server.Api;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

namespace Jajcarz.Server.Hosting;

/// <summary>Logowanie, CSRF i nagłówki bezpieczeństwa strony publicznej.</summary>
public static class WebSecurity
{
    private const string ContentSecurityPolicy =
        "default-src 'self'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'; object-src 'none'";

    private const int LoginAttemptsPerMinute = 10;

    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(365);

    public static IServiceCollection AddJajcarzWebSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(ConfigureCookie);
        services.AddAuthorization();

        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "jajcarz.csrf";
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        // Bez trwałych kluczy nowy kontener wylogowałby wszystkich. Lokalnie trzyma je profil użytkownika.
        var keyRingPath = configuration["DataProtection:KeyRingPath"];
        if (!string.IsNullOrWhiteSpace(keyRingPath))
        {
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(keyRingPath))
                .SetApplicationName("Jajcarz");
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            // Adres Caddy w sieci kontenerów nie jest stały, a aplikacja nie ma publicznego portu.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(SessionEndpoints.LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = LoginAttemptsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                }));
        });
        return services;
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use((http, next) =>
        {
            var headers = http.Response.Headers;
            headers["Content-Security-Policy"] = ContentSecurityPolicy;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            return next();
        });

    private static void ConfigureCookie(CookieAuthenticationOptions options)
    {
        options.Cookie.Name = "jajcarz.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = SessionLifetime;
        options.SlidingExpiration = true;

        // API nie ma strony logowania. Tę pokazuje aplikacja po kodzie 401.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = ValidateSessionAsync;
    }

    /// <summary>Cookie żyje rok, więc zmiana hasła musi móc je unieważnić, na przykład po zgubieniu telefonu.</summary>
    private static async Task ValidateSessionAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var accounts = context.HttpContext.RequestServices.GetRequiredService<AccountService>();
        var valid = principal?.UserId() is { } userId
            && principal.SessionStamp() is { } stamp
            && await accounts.IsSessionValidAsync(userId, stamp, context.HttpContext.RequestAborted);
        if (!valid)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync();
        }
    }
}
