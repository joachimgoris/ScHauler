using Microsoft.EntityFrameworkCore;
using ScHauler.Data.Converters;
using ScHauler.Models;

namespace ScHauler.Data;

public sealed class HaulerDbContext(DbContextOptions<HaulerDbContext> options) : DbContext(options)
{
    public DbSet<Contract> Contracts => Set<Contract>();

    public DbSet<CargoLine> CargoLines => Set<CargoLine>();

    public DbSet<Location> Locations => Set<Location>();

    public DbSet<Ship> Ships => Set<Ship>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<ContractId>().HaveConversion<ContractIdConverter>();
        configurationBuilder.Properties<CargoLineId>().HaveConversion<CargoLineIdConverter>();
        configurationBuilder.Properties<CargoContainerId>().HaveConversion<CargoContainerIdConverter>();
        configurationBuilder.Properties<LocationId>().HaveConversion<LocationIdConverter>();
        configurationBuilder.Properties<ShipId>().HaveConversion<ShipIdConverter>();
        configurationBuilder.Properties<ShipBayId>().HaveConversion<ShipBayIdConverter>();
        configurationBuilder.Properties<ContainerSize>().HaveConversion<ContainerSizeConverter>();
        configurationBuilder.Properties<Scu>().HaveConversion<ScuConverter>();
        configurationBuilder.Properties<Placement>().HaveConversion<PlacementConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HaulerDbContext).Assembly);
    }
}