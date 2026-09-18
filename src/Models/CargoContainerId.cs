namespace ScHauler.Models;

public readonly record struct CargoContainerId(Guid Value)
{
    public static CargoContainerId New() => new(Guid.CreateVersion7());
}