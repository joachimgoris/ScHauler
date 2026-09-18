namespace ScHauler.Models;

public readonly record struct ContractId(Guid Value)
{
    public static ContractId New() => new(Guid.CreateVersion7());
}
