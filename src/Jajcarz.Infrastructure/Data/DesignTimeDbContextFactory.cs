using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Jajcarz.Infrastructure.Data;

/// <summary>Dla dotnet ef. Migracje dotyczą Postgresa, a generowanie nie łączy się z bazą.</summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<JajcarzDbContext>
{
    public JajcarzDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<JajcarzDbContext>().UseNpgsql("Host=localhost").Options);
}
