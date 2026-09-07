using ScHauler.Models;

namespace ScHauler.Services;

public sealed record BoxCount(ContainerSize Size, int Count);

public sealed record StopAction(CargoLineId CargoLineId, ActionKind Kind, string Commodity, int Scu, string ContractName, IReadOnlyList<BoxCount> Boxes);

public sealed record LocationStop(string LocationName, IReadOnlyList<StopAction> Actions)
{
    public int PickupScu => Actions.Where(a => a.Kind == ActionKind.Pickup).Sum(a => a.Scu);
    public int DeliverScu => Actions.Where(a => a.Kind == ActionKind.Deliver).Sum(a => a.Scu);
}

public sealed record HoldBoxView(int X, int Y, int Z, int Length, int Width, int Scu, int Height, string DropOffName);

public sealed record HoldBayView(string Name, int Length, int Width, int Height, int DrawOffsetX, int DrawOffsetY, IReadOnlyList<HoldBoxView> Boxes);

public sealed record HoldView(IReadOnlyList<HoldBayView> Bays, int UnplacedBoxes);

/// <summary>
/// Pure logic: turns cargo lines into per-location stops with actionable work.
/// A Pending line is actionable at its pickup location; a PickedUp line at its drop off.
/// Delivered lines are out of the picture.
/// </summary>
public static class ManifestPlanner
{
    public static IReadOnlyList<LocationStop> BuildStops(IEnumerable<CargoLine> lines) =>
        lines
            .Where(l => l.Status != CargoLineStatus.Delivered)
            .Select(l => (
                Location: l.Status == CargoLineStatus.Pending ? l.PickupLocation.Name : l.DropOffLocation.Name,
                Action: new StopAction(
                    l.Id,
                    l.Status == CargoLineStatus.Pending ? ActionKind.Pickup : ActionKind.Deliver,
                    l.Commodity,
                    l.Scu.Value,
                    l.Contract.Name,
                    BoxBreakdown(l))))
            .GroupBy(x => x.Location)
            .Select(g => new LocationStop(
                g.Key,
                g.Select(x => x.Action)
                    .OrderBy(a => a.Kind == ActionKind.Deliver ? 0 : 1)
                    .ThenBy(a => a.ContractName, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .OrderBy(s => s.LocationName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static int OnboardScu(IEnumerable<CargoLine> lines) =>
        lines.Where(l => l.Status == CargoLineStatus.PickedUp).Sum(l => l.Scu.Value);

    public static HoldView BuildHoldView(IReadOnlyList<ShipBay> bays, IEnumerable<CargoContainer> aboard)
    {
        ArgumentNullException.ThrowIfNull(bays);
        ArgumentNullException.ThrowIfNull(aboard);

        var placed = new List<(ShipBayId BayId, HoldBoxView Box)>();
        var unplaced = 0;
        foreach (var container in aboard)
        {
            if (container.Placement is not { } p)
            {
                unplaced++;
                continue;
            }

            var length = p.Rotated ? container.Size.Width : container.Size.Length;
            var width = p.Rotated ? container.Size.Length : container.Size.Width;
            placed.Add((p.BayId, new HoldBoxView(p.X, p.Y, p.Z, length, width, container.Size.Value, container.Size.Height, container.Line.DropOffLocation.Name)));
        }

        var bayViews = bays.Select(b => new HoldBayView(b.Name, b.Length, b.Width, b.Height, b.DrawOffsetX, b.DrawOffsetY, [.. placed.Where(x => x.BayId == b.Id).Select(x => x.Box).OrderBy(x => x.Z)])).ToList();

        return new HoldView(bayViews, unplaced);
    }

    private static List<BoxCount> BoxBreakdown(CargoLine line) => [.. line.Containers.GroupBy(c => c.Size).OrderByDescending(g => g.Key.Value).Select(g => new BoxCount(g.Key, g.Count()))];
}