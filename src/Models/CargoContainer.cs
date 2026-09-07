namespace ScHauler.Models;

public sealed class CargoContainer
{
    private CargoContainer(CargoContainerId id, CargoLineId cargoLineId, ContainerSize size)
    {
        Id = id;
        CargoLineId = cargoLineId;
        Size = size;
    }

    public CargoContainerId Id { get; }

    public CargoLineId CargoLineId { get; }

    public CargoLine Line { get; private set; } = null!;

    public ContainerSize Size { get; }

    /// <summary>
    /// Null = not (yet) placed. Set from the stowage plan at pickup time; frozen thereafter (recomputes never move a placed box).
    /// </summary>
    public Placement? Placement { get; private set; }

    /// <summary>
    /// Placement describes a box in the hold, so the owning line mut be PickedUp.
    /// </summary>
    public void Place(Placement placement)
    {
        if (Line.Status != CargoLineStatus.PickedUp)
        {
            throw new InvalidOperationException("A placement can only be set while the container is aboard.");
        }

        Placement = placement;
    }

    internal void ClearPlacement() => Placement = null;

    internal static CargoContainer Create(CargoLine line, ContainerSize size)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(size);

        return new CargoContainer(CargoContainerId.New(), line.Id, size)
        {
            Line = line,
        };
    }
}