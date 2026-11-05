using Jajcarz.Application.Accounts;
using Jajcarz.Application.Panels;
using Jajcarz.Application.Products;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Jajcarz.Application;

public static class ApplicationServices
{
    public static IServiceCollection AddJajcarzApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<PanelLinks>();
        services.AddSingleton<ProductEvents>();
        services.AddScoped<AccountService>();
        services.AddScoped<SnapshotBuilder>();
        services.AddScoped<PanelService>();
        services.AddScoped<ProductService>();
        return services;
    }
}
