namespace ScHauler.Models;

public readonly record struct ShipId(Guid Value)
{
    public static ShipId New() => new(Guid.CreateVersion7());
}