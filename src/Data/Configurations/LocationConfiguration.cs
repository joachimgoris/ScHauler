using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScHauler.Models;

namespace ScHauler.Data.Configurations;

public sealed class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Name);
        builder.Property(l => l.Kind);
        builder.Property(l => l.ParentId);

        builder.HasOne<Location>().WithMany().HasForeignKey(l => l.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}