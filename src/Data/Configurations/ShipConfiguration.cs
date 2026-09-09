using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScHauler.Models;

namespace ScHauler.Data.Configurations;

public sealed class ShipConfiguration : IEntityTypeConfiguration<Ship>
{
    public void Configure(EntityTypeBuilder<Ship> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(s => s.Id);

        builder.HasMany(s => s.Bays)
            .WithOne(b => b.Ship)
            .HasForeignKey(b => b.ShipId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.Bays)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
    }
}