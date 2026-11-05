using Jajcarz.Domain.Panels;
using Jajcarz.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Jajcarz.Infrastructure.Data;

public sealed class JajcarzDbContext(DbContextOptions<JajcarzDbContext> options) : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();

    public DbSet<PanelKey> PanelKeys => Set<PanelKey>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<RevCounter> RevCounters => Set<RevCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JajcarzDbContext).Assembly);
}
