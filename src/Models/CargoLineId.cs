namespace ScHauler.Models;

public readonly record struct CargoLineId(Guid Value)
{
    public static CargoLineId New() => new(Guid.CreateVersion7());
}