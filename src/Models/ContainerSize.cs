namespace ScHauler.Models;

/// <summary>
/// Closed set of of cargo box sizes the game ships. Smart enum: private ctor and fixed instances make invalid sizes unrepresentable (and there is no default-struct hole, unlike a record struct.)
/// </summary>
public sealed class ContainerSize
{
    private ContainerSize(int value, int length, int width, int height)
    {
        Value = value;
        Length = length;
        Width = width;
        Height = height;
    }

    public static ContainerSize One { get; } = new(1, 1, 1, 1);

    public static ContainerSize Two { get; } = new(2, 2, 1, 1);

    public static ContainerSize Four { get; } = new(4, 2, 2, 1);

    public static ContainerSize Eight { get; } = new(8, 2, 2, 2);

    public static ContainerSize Sixteen { get; } = new(16, 4, 2, 2);

    public static ContainerSize TwentyFour { get; } = new(24, 6, 2, 2);

    public static ContainerSize ThirtyTwo { get; } = new(32, 8, 2, 2);

    /// <summary>
    /// Ascending, for the contract-entry tap counters.
    /// </summary>
    public static IReadOnlyList<ContainerSize> All { get; } = [One, Two, Four, Eight, Sixteen, TwentyFour, ThirtyTwo];

    /// <summary>
    /// SCU capacity of the box.
    /// </summary>
    public int Value { get; }

    /// <summary>
    /// Footprint in 1-SCU cells (1 cell = 1,25m). Length is the long axis; horizontal rotation (length*width swap) is a placement concern, height never rotates.
    /// </summary>
    public int Length { get; }

    public int Width { get; }

    public int Height { get; }

    public static ContainerSize FromScu(int value) => value switch
    {
        1 => One,
        2 => Two,
        4 => Four,
        8 => Eight,
        16 => Sixteen,
        24 => TwentyFour,
        32 => ThirtyTwo,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Container size must be 1, 2, 4, 8, 16, 24 or 32 SCU."),
    };


    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}