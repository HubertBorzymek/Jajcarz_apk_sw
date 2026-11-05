using System.Security.Claims;

namespace Jajcarz.Server.Api;

/// <summary>Dane zalogowanego użytkownika zapisane w cookie.</summary>
public static class ApiUser
{
    public const string SessionStampClaim = "jajcarz:session";

    public static string Actor(this ClaimsPrincipal user) => $"web:{user.Identity!.Name}";

    public static Guid? UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static Guid? SessionStamp(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(SessionStampClaim), out var stamp) ? stamp : null;
}
