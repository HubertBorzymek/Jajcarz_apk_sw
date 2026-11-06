using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace Jajcarz.Server.Hosting;

/// <summary>Pliki z /assets mają skrót w nazwie, a reszta musi się odświeżać po każdym wdrożeniu.</summary>
public static class StaticFileCaching
{
    private static readonly TimeSpan AssetLifetime = TimeSpan.FromDays(365);

    public static StaticFileOptions Options() => new()
    {
        ContentTypeProvider = ContentTypes(),
        OnPrepareResponse = context => Apply(context.Context),
    };

    private static FileExtensionContentTypeProvider ContentTypes()
    {
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".webmanifest"] = "application/manifest+json";
        return provider;
    }

    private static void Apply(HttpContext http)
    {
        var immutable = http.Request.Path.StartsWithSegments("/assets");
        http.Response.GetTypedHeaders().CacheControl = new CacheControlHeaderValue
        {
            Public = immutable,
            NoCache = !immutable,
            MaxAge = immutable ? AssetLifetime : null,
        };
    }
}
