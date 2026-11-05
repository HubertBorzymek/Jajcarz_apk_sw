using Jajcarz.Application.Abstractions;
using Jajcarz.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jajcarz.Infrastructure;

public static class InfrastructureServices
{
    private const string ConnectionName = "Jajcarz";
    private const string ProviderKey = "Database:Provider";
    private const string SqliteProvider = "Sqlite";

    /// <summary>Produkcja używa Postgresa. SQLite służy do pracy lokalnej i testów, bo nie wymaga instalacji.</summary>
    public static IServiceCollection AddJajcarzInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<WriteGate>();
        services.AddScoped<IStore, EfStore>();
        services.AddDbContext<JajcarzDbContext>(options =>
        {
            var connection = configuration.GetConnectionString(ConnectionName)
                ?? throw new InvalidOperationException($"Brak ConnectionStrings:{ConnectionName}");
            if (configuration[ProviderKey] == SqliteProvider)
            {
                options.UseSqlite(connection);
            }
            else
            {
                options.UseNpgsql(connection);
            }
        });
        return services;
    }

    public static async Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<JajcarzDbContext>();
        if (db.Database.IsNpgsql())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
    }
}
