using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScHauler.Models;

namespace ScHauler.Data.Configurations;

public sealed class ContractConfiguration : IEntityTypeConfiguration<Contract>
{
    public void Configure(EntityTypeBuilder<Contract> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name);
        builder.Property(c => c.RewardAuec);
        builder.Property(c => c.AcceptedAt);

        builder.HasMany(c => c.Lines)
            .WithOne(l => l.Contract)
            .HasForeignKey(l => l.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        // Bind the encapsulated collection to its backing field.
        builder.Navigation(c => c.Lines)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}