using ScHauler.Models;

namespace ScHauler.Services;

public sealed record BoxCount(ContainerSize Size, int Count);

public sealed record StopAction(CargoLineId CargoLineId, ActionKind Kind, string Commodity, int Scu, string ContractName, IReadOnlyList<BoxCount> Boxes);

public sealed record LocationStop(LocationId LocationId, string LocationName, IReadOnlyList<StopAction> Actions)
{
    public int PickupScu => Actions.Where(a => a.Kind == ActionKind.Pickup).Sum(a => a.Scu);
    public int DeliverScu => Actions.Where(a => a.Kind == ActionKind.Deliver).Sum(a => a.Scu);
}

public sealed record HoldBoxView(int X, int Y, int Z, int Length, int Width, int Scu, int Height, string DropOffName);

public sealed record HoldBayView(string Name, int Length, int Width, int Height, int DrawOffsetX, int DrawOffsetY, IReadOnlyList<HoldBoxView> Boxes);

/// <summary>
/// One reserved column of the suggested stow, in canvas cells (bay draw offset applied), with its tower height.
/// </summary>
public sealed record HoldGhostView(int DrawX, int DrawY, int Height, string DropOffName);

public sealed record HoldView(IReadOnlyList<HoldBayView> Bays, int UnplacedBoxes, IReadOnlyList<HoldGhostView> Ghosts);

public sealed record Destination(LocationId Id, string Name, int AboardScu, int PendingScu);

/// <summary>
/// An earlier drop whose nearest-to-ramp box sits deeper than a later drop's in the same bay and lane: it is buried behind cargo that leaves the ship later.
/// </summary>
public sealed record StowConflict(string BlockedDropOff, string BlockingDropOff);

/// <summary>
/// Pure logic: turns cargo lines into per-location stops with actionable work.
/// A Pending line is actionable at its pickup location; a PickedUp line at its drop off.
/// Delivered lines are out of the picture.
/// </summary>
public static class ManifestPlanner
{
    /// <summary>
    /// Onboard fraction of capacity at which the tiebreak flips from alphabetical to "free the most SCU first".
    /// </summary>
    private const double NearCapacityThreshold = 0.8;

    /// <summary>
    /// Greedy nearest-first suggestion: repeatedly visit the closest remaining stop (hop distance), simulating onboard SCU per visit. Ties: when near capacity, the stop freeing the most SCU; otherwise alphabetical. Falls back to the alphabetical list when the current location is unknown.
    /// </summary>
    public static IReadOnlyList<LocationStop> BuildStops(
        IEnumerable<CargoLine> lines,
        RouteNet net,
        LocationId? currentLocationId,
        int onboardScu,
        int capacityScu)
    {
        ArgumentNullException.ThrowIfNull(net);

        return OrderByRoute(BuildStops(lines), net, currentLocationId, onboardScu, capacityScu);
    }

    private static IReadOnlyList<LocationStop> OrderByRoute(
        IReadOnlyList<LocationStop> stops,
        RouteNet net,
        LocationId? currentLocationId,
        int onboardScu,
        int capacityScu)
    {
        if (currentLocationId is null)
        {
            return stops;
        }

        var remaining = stops.ToList();
        var ordered = new List<LocationStop>(remaining.Count);
        var position = currentLocationId.Value;
        var onboard = onboardScu;

        while (remaining.Count > 0)
        {
            var nearCapacity = capacityScu > 0 && onboard >= capacityScu * NearCapacityThreshold;
            var next = remaining.OrderBy(s => net.Hops(position, s.LocationId))
            .ThenByDescending(s => nearCapacity ? s.DeliverScu : 0)
            .ThenBy(s => s.LocationName, StringComparer.OrdinalIgnoreCase)
            .First();

            ordered.Add(next);
            remaining.Remove(next);
            position = next.LocationId;
            onboard = Math.Max(0, onboard - next.DeliverScu) + next.PickupScu;
        }

        return ordered;
    }

    public static IReadOnlyList<LocationStop> BuildStops(IEnumerable<CargoLine> lines) =>
        lines
            .Where(l => l.Status != CargoLineStatus.Delivered)
            .Select(l => (
                Location: l.Status == CargoLineStatus.Pending ? l.PickupLocation : l.DropOffLocation,
                Action: new StopAction(
                    l.Id,
                    l.Status == CargoLineStatus.Pending ? ActionKind.Pickup : ActionKind.Deliver,
                    l.Commodity,
                    l.Scu.Value,
                    l.Contract.Name,
                    BoxBreakdown(l))))
            .GroupBy(x => x.Location.Id)
            .Select(g => new LocationStop(
                g.Key,
                g.First().Location.Name,
                g.Select(x => x.Action)
                    .OrderBy(a => a.Kind == ActionKind.Deliver ? 0 : 1)
                    .ThenBy(a => a.ContractName, StringComparer.OrdinalIgnoreCase)
                    .ToList()))
            .OrderBy(s => s.LocationName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Every drop-off of a non-delivered line, aboard or pending, ordered by the stops' greedy nearest-first heuristic run over drop-offs only (pickups are not visited, so this can differ from the stop list) - the point is that the order does not flip when a line is picked up. A manual order is kept as-is (stale ids dropped) with newcomers appended.
    /// </summary>
    public static IReadOnlyList<Destination> DropOrder(
        IEnumerable<CargoLine> lines,
        RouteNet net,
        LocationId? currentLocationId,
        int onboardScu,
        int capacityScu,
        IReadOnlyList<LocationId>? manual)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var byDropOff = lines
            .Where(l => l.Status != CargoLineStatus.Delivered)
            .GroupBy(l => l.DropOffLocationId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Synthetic deliver-only stops: the route heuristic only looks at location and DeliverScu.
        var asStops = byDropOff
            .Select(kv => new LocationStop(
                kv.Key,
                kv.Value[0].DropOffLocation.Name,
                [.. kv.Value.Select(l => new StopAction(l.Id, ActionKind.Deliver, l.Commodity, l.Scu.Value, l.Contract.Name, []))]))
            .OrderBy(s => s.LocationName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var defaults = OrderByRoute(asStops, net, currentLocationId, onboardScu, capacityScu)
            .Select(s => new Destination(
                s.LocationId,
                s.LocationName,
                byDropOff[s.LocationId].Where(l => l.Status == CargoLineStatus.PickedUp).Sum(l => l.Scu.Value),
                byDropOff[s.LocationId].Where(l => l.Status == CargoLineStatus.Pending).Sum(l => l.Scu.Value)))
            .ToList();

        if (manual is null)
        {
            return defaults;
        }

        var byId = defaults.ToDictionary(d => d.Id);
        var kept = manual.Where(byId.ContainsKey).Distinct().Select(id => byId[id]).ToList();
        return [.. kept, .. defaults.Where(d => !manual.Contains(d.Id))];
    }

    public static IReadOnlyList<StowConflict> StowCheck(HoldView hold, IReadOnlyList<string> dropOrder)
    {
        ArgumentNullException.ThrowIfNull(hold);
        ArgumentNullException.ThrowIfNull(dropOrder);

        // Nearest-to-ramp X per destination per (bay, lane); only overlapping lanes of the same bay can bury each other - the walkway reaches every bay.
        var nearest = hold.Bays
            .SelectMany(bay => bay.Boxes.SelectMany(b => Enumerable.Range(0, b.Width)
                .Select(dy => (b.DropOffName, Lane: (bay.Name, b.Y + dy), Depth: b.X))))
            .GroupBy(x => x.DropOffName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(x => x.Lane).ToDictionary(l => l.Key, l => l.Min(x => x.Depth)),
                StringComparer.OrdinalIgnoreCase);

        var aboard = dropOrder.Where(nearest.ContainsKey).ToList();
        return [.. aboard.SelectMany((earlier, i) => aboard
            .Skip(i + 1)
            .Where(later => nearest[earlier].Any(lane => nearest[later].TryGetValue(lane.Key, out var d) && lane.Value > d))
            .Select(later => new StowConflict(earlier, later)))];
    }

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

        return new HoldView(bayViews, unplaced, []);
    }

    public static IReadOnlyList<HoldGhostView> BuildGhosts(
        IReadOnlyList<ShipBay> bays,
        IReadOnlyDictionary<LocationId, IReadOnlySet<HoldCell>> regions,
        IReadOnlyList<Destination> destinations)
    {
        ArgumentNullException.ThrowIfNull(bays);
        ArgumentNullException.ThrowIfNull(regions);
        ArgumentNullException.ThrowIfNull(destinations);

        var baysById = bays.ToDictionary(b => b.Id);
        return [.. destinations
            .Where(d => regions.ContainsKey(d.Id))
            .SelectMany(d => regions[d.Id]
                .GroupBy(c => (c.BayId, c.X, c.Y))
                .Select(g => new HoldGhostView(
                    baysById[g.Key.BayId].DrawOffsetX + g.Key.X,
                    baysById[g.Key.BayId].DrawOffsetY + g.Key.Y,
                    g.Count(),
                    d.Name)))
            .OrderBy(g => g.DrawX)
            .ThenBy(g => g.DrawY)];
    }

    private static List<BoxCount> BoxBreakdown(CargoLine line) => [.. line.Containers.GroupBy(c => c.Size).OrderByDescending(g => g.Key.Value).Select(g => new BoxCount(g.Key, g.Count()))];
}