using System.Text.Json;
using Jajcarz.Domain.Panels;
using Jajcarz.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jajcarz.Infrastructure.Data;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    private const int DeviceIdLength = 12;
    private const int Sha256HexLength = 64;
    private const int MaxFirmwareLength = 64;

    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.HasKey(device => device.Id);
        builder.Property(device => device.Id).HasMaxLength(DeviceIdLength);
        builder.Property(device => device.TokenHash).HasMaxLength(Sha256HexLength);
        builder.HasIndex(device => device.TokenHash).IsUnique();
        builder.Property(device => device.Firmware).HasMaxLength(MaxFirmwareLength);
        builder.Property(device => device.Config).HasConversion(
            config => JsonSerializer.Serialize(config, JsonSerializerOptions.Default),
            text => JsonSerializer.Deserialize<PanelConfig>(text, JsonSerializerOptions.Default)!);
    }
}

internal sealed class PanelKeyConfiguration : IEntityTypeConfiguration<PanelKey>
{
    private const int MaxAddrLength = 7;    // "255.255"

    public void Configure(EntityTypeBuilder<PanelKey> builder)
    {
        builder.HasKey(key => new { key.DeviceId, key.Addr });
        builder.Property(key => key.Addr).HasMaxLength(MaxAddrLength);
        builder.HasIndex(key => key.ProductId).IsUnique();
        builder.HasOne<Device>().WithMany().HasForeignKey(key => key.DeviceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Product>().WithMany().HasForeignKey(key => key.ProductId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    private const int MaxStateLength = 3;
    private const int MaxActorLength = 80;
    private const int MaxChangeKeyLength = 100;

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasKey(product => product.Id);
        builder.Property(product => product.Name).HasMaxLength(Product.MaxNameLength);
        builder.Property(product => product.Category).HasMaxLength(Product.MaxCategoryLength);
        builder.Property(product => product.State).HasConversion<string>().HasMaxLength(MaxStateLength);
        builder.Property(product => product.ChangedBy).HasMaxLength(MaxActorLength);
        builder.Property(product => product.LastChangeKey).HasMaxLength(MaxChangeKeyLength);
    }
}

internal sealed class RevCounterConfiguration : IEntityTypeConfiguration<RevCounter>
{
    public void Configure(EntityTypeBuilder<RevCounter> builder)
    {
        builder.HasKey(counter => counter.Id);
        builder.Property(counter => counter.Id).ValueGeneratedNever();
        builder.HasData(new RevCounter { Id = RevCounter.SingleId, Value = 0 });
    }
}
