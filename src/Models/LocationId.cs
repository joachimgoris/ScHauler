namespace ScHauler.Models;

public readonly record struct LocationId(Guid Value)
{
    public static LocationId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}