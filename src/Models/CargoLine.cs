namespace ScHauler.Models;

public sealed class CargoLine
{
    private readonly List<CargoContainer> _containers = [];

    private CargoLine(
        CargoLineId id,
        ContractId contractId,
        Scu scu,
        string commodity,
        LocationId pickupLocationId,
        LocationId dropOffLocationId)
    {
        Id = id;
        ContractId = contractId;
        Scu = scu;
        Commodity = commodity;
        PickupLocationId = pickupLocationId;
        DropOffLocationId = dropOffLocationId;
        Status = CargoLineStatus.Pending;
    }

    public CargoLineId Id { get; }

    public ContractId ContractId { get; }

    public Contract Contract { get; private set; } = null!;

    public Scu Scu { get; }

    public string Commodity { get; }

    public IReadOnlyCollection<CargoContainer> Containers => _containers.AsReadOnly();

    public LocationId PickupLocationId { get; }

    public Location PickupLocation { get; private set; } = null!;

    public LocationId DropOffLocationId { get; }

    public Location DropOffLocation { get; private set; } = null!;

    public CargoLineStatus Status { get; private set; }

    internal static CargoLine Create(
        Contract contract,
        string commodity,
        Scu scu,
        Location pickup,
        Location dropOff)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(pickup);
        ArgumentNullException.ThrowIfNull(dropOff);
        ArgumentException.ThrowIfNullOrWhiteSpace(commodity);

        if (pickup.Id == dropOff.Id)
        {
            throw new ArgumentException("Pickup and dropOff must be different locations.", nameof(dropOff));
        }

        var line = new CargoLine(CargoLineId.New(), contract.Id, scu, commodity.Trim(), pickup.Id, dropOff.Id)
        {
            Contract = contract,
            PickupLocation = pickup,
            DropOffLocation = dropOff,
        };

        return line;
    }

    /// <summary>
    /// The boxes revealed at the pickup point. Strict: they must sum to the contracted SCU. Replaces any previously entered containers.
    /// </summary>
    public void PickUp(IReadOnlyList<ContainerSize> sizes)
    {
        ArgumentNullException.ThrowIfNull(sizes);

        if (Status != CargoLineStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending line can be picked up.");
        }

        if (sizes.Count == 0)
        {
            throw new ArgumentException("At least one container is required.", nameof(sizes));
        }

        var total = 0;
        foreach (var size in sizes)
        {
            ArgumentNullException.ThrowIfNull(size, nameof(sizes));
            total += size.Value;
        }

        if (total != Scu.Value)
        {
            throw new ArgumentException($"Boxes sum to {total} SCU; the contract line says {Scu.Value} SCU.", nameof(sizes));
        }

        _containers.Clear();
        foreach (var size in sizes)
        {
            _containers.Add(CargoContainer.Create(this, size));
        }

        Status = CargoLineStatus.PickedUp;
    }

    /// <summary>
    /// PickedUp → Delivered. Pending leaves via PickUp; Delivered is terminal.
    /// </summary>
    public void Advance() => Status = Status switch
    {
        CargoLineStatus.PickedUp => CargoLineStatus.Delivered,
        CargoLineStatus.Pending => throw new InvalidOperationException("A pending line is picked up via PickUp with its box sizes."),
        _ => throw new InvalidOperationException("A delivered line cannot advance."),
    };

    /// <summary>
    /// Manual correction (contracts-page stepper / mis-tap undo). Deliberate bypasses the flow. Containers are kept.
    /// </summary>
    public void Correct(CargoLineStatus status)
    {
        if (status == CargoLineStatus.Pending)
        {
            foreach (var container in _containers)
            {
                container.ClearPlacement();
            }
        }

        Status = status;
    }
}