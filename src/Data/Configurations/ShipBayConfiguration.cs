using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScHauler.Models;

namespace ScHauler.Data.Configurations;

public sealed class ShipBayConfiguration : IEntityTypeConfiguration<ShipBay>
{
    public void Configure(EntityTypeBuilder<ShipBay> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(b => b.Id);
        builder.ToTable("ShipBays");

        builder.Property(b => b.Name);
        builder.Property(b => b.Length);
        builder.Property(b => b.Width);
        builder.Property(b => b.Height);
        builder.Property(b => b.DrawOffsetX);
        builder.Property(b => b.DrawOffsetY);
    }
}