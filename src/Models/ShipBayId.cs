namespace ScHauler.Models;

public readonly record struct ShipBayId(Guid Value)
{
    public static ShipBayId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}