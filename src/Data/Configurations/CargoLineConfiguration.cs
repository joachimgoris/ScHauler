using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScHauler.Models;

namespace ScHauler.Data.Configurations;

public sealed class CargoLineConfiguration : IEntityTypeConfiguration<CargoLine>
{
    public void Configure(EntityTypeBuilder<CargoLine> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Commodity);
        builder.Property(l => l.Scu);

        builder.HasOne(l => l.PickupLocation)
            .WithMany()
            .HasForeignKey(l => l.PickupLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(l => l.DropOffLocation)
            .WithMany()
            .HasForeignKey(l => l.DropOffLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(l => l.Containers)
            .WithOne(c => c.Line)
            .HasForeignKey(c => c.CargoLineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(l => l.Containers)
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .AutoInclude();
    }
}
