namespace ScHauler.Models;

public sealed class ShipBay
{
    private ShipBay(ShipBayId id, ShipId shipId, string name, int length, int width, int height, int drawOffsetX, int drawOffsetY)
    {
        Id = id;
        ShipId = shipId;
        Name = name;
        Length = length;
        Width = width;
        Height = height;
        DrawOffsetX = drawOffsetX;
        DrawOffsetY = drawOffsetY;
    }

    public ShipBayId Id { get; }

    public ShipId ShipId { get; }

    public Ship Ship { get; private set; } = null!;

    public string Name { get; }

    public int Length { get; }

    public int Width { get; }

    public int Height { get; }

    public int DrawOffsetX { get; }

    public int DrawOffsetY { get; }

    internal static ShipBay Create(Ship ship, string name, int length, int width, int height, int drawOffsetX, int drawOffsetY)
    {
        ArgumentNullException.ThrowIfNull(ship);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegative(drawOffsetX);
        ArgumentOutOfRangeException.ThrowIfNegative(drawOffsetY);

        return new ShipBay(ShipBayId.New(), ship.Id, name, length, width, height, drawOffsetX, drawOffsetY)
        {
            Ship = ship
        };
    }
}