using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScHauler.Models;

namespace ScHauler.Data.Configurations;

public sealed class CargoContainerConfiguration : IEntityTypeConfiguration<CargoContainer>
{
    public void Configure(EntityTypeBuilder<CargoContainer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("CargoContainers");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Size);
        builder.Property(c => c.Placement);
    }
}