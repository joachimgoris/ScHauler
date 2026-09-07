namespace ScHauler.Models;

public readonly record struct Scu
{
    public Scu(int value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    public int Value { get; }

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}