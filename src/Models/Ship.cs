using System.Diagnostics.CodeAnalysis;

namespace ScHauler.Models;

public sealed class Ship
{
    private readonly List<ShipBay> _bays = [];

    private Ship(ShipId id, string name, int cargoCapacityScu)
    {
        Id = id;
        Name = name;
        CargoCapacityScu = cargoCapacityScu;
    }

    public ShipId Id { get; }

    public IReadOnlyCollection<ShipBay> Bays => _bays.AsReadOnly();

    private string _name;
    public string Name
    {
        get
        {
            return _name;
        }
        [MemberNotNull(nameof(_name))]
        private set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _name = value.Trim();
        }
    }


    private int _cargoCapacityScu;
    /// <summary>
    /// Cargo capacity in SCU. 0 = not set, capacity bar hidden.
    /// </summary>
    public int CargoCapacityScu
    {
        get
        {
            return _cargoCapacityScu;
        }
        private set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _cargoCapacityScu = value;
        }
    }

    public static Ship Create(string name, int cargoCapacityScu = 0) => new(ShipId.New(), name, cargoCapacityScu);

    public void SetCapacity(int cargoCapacityScu) => CargoCapacityScu = cargoCapacityScu;

    public ShipBay AddBay(string name, int length, int width, int height, int drawOffsetX, int drawOffsetY)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();
        if (_bays.Any(b => string.Equals(b.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"Bay '{trimmed}' already exists on {Name}.", nameof(name));
        }

        var bay = ShipBay.Create(this, trimmed, length, width, height, drawOffsetX, drawOffsetY);
        _bays.Add(bay);
        return bay;
    }
}