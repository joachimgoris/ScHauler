using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ScHauler.Models;

namespace ScHauler.Data.Converters;

public sealed class ContractIdConverter() : ValueConverter<ContractId, Guid>(
    id => id.Value,
    value => new ContractId(value));

public sealed class CargoLineIdConverter() : ValueConverter<CargoLineId, Guid>(
    id => id.Value,
    value => new CargoLineId(value));

public sealed class CargoContainerIdConverter() : ValueConverter<CargoContainerId, Guid>(
    id => id.Value,
    value => new CargoContainerId(value));

public sealed class LocationIdConverter() : ValueConverter<LocationId, Guid>(
    id => id.Value,
    value => new LocationId(value));

public sealed class ShipIdConverter() : ValueConverter<ShipId, Guid>(
    id => id.Value,
    value => new ShipId(value));

public sealed class ShipBayIdConverter() : ValueConverter<ShipBayId, Guid>(
    id => id.Value,
    value => new ShipBayId(value));

public sealed class ContainerSizeConverter() : ValueConverter<ContainerSize, int>(
    size => size.Value,
    value => ContainerSize.FromScu(value));

public sealed class ScuConverter() : ValueConverter<Scu, int>(
    scu => scu.Value,
    value => new Scu(value));

public sealed class PlacementConverter() : ValueConverter<Placement, string>(
    p => $"{p.BayId.Value}:{p.X}:{p.Y}:{p.Z}:{(p.Rotated ? 1 : 0)}",
    s => Parse(s))
{
    private static Placement Parse(string value)
    {
        var parts = value.Split(':');
        return new Placement(
            new ShipBayId(Guid.Parse(parts[0])),
            int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
            int.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture),
            parts[4] == "1");
    }
}
