using ScHauler.Models;

namespace ScHauler.Services;

public enum StowTier
{
    /// <summary>Boxes placed with the moat kept.</summary>
    Planned,

    /// <summary>Boxes placed, but piles touch side by side.</summary>
    Tight,

    /// <summary>The whole hold was re-planned from empty, moat kept.</summary>
    Restowed,

    /// <summary>The whole hold was re-planned and piles touch.</summary>
    RestowedTight,
}

/// <summary>Placements for the new boxes; for a restow, for every box aboard too.</summary>
public sealed record TieredPlan(StowTier Tier, IReadOnlyDictionary<CargoContainerId, Placement> Placements);

/// <summary>
/// Pure stowage planning. Rules: a box fits inside one bay (walkways are bay boundaries); no overlap; support is the floor or full SAME-destination support; different destinations never touch - the footprint expanded by one cell in X/Y must hold no foreign box at any level - unless <c>allowTouching</c> (tight stow, used only when the load would otherwise be refused). Placements are proposed one at pickup and frozen; existing placements are immovable input.
/// Deterministic: candidates that do not sit in front of an earlier drop's cargo (given the optional drop order) win first, then the fewest cells outside the optional reserved region (the forecast); then bays in order, lowest Z, X, Y; candidates touching the own-destination pile win over free-standing ones; unrotated before rotated. Returns null when the line cannot fit (atomic - no partial plans).
/// </summary>
public static class StowagePlanner
{
    public static IReadOnlyDictionary<CargoContainerId, Placement>? Plan(
        IReadOnlyList<ShipBay> bays,
        IReadOnlyList<CargoContainer> aboard,
        CargoLine pickedUpLine,
        IReadOnlySet<HoldCell>? reserved = null,
        IReadOnlyList<LocationId>? dropOrder = null,
        bool allowTouching = false)
    {
        ArgumentNullException.ThrowIfNull(bays);
        ArgumentNullException.ThrowIfNull(aboard);
        ArgumentNullException.ThrowIfNull(pickedUpLine);

        var placed = aboard
            .Where(c => c.Placement is not null)
            .Select(c => new PlacedBox(c.Line.DropOffLocationId, c.Size, c.Placement!.Value))
            .ToList();

        return PlanBoxes(bays, placed, pickedUpLine.Containers.Where(c => c.Placement is null).Select(ToBox), pickedUpLine.DropOffLocationId, reserved, dropOrder, allowTouching);
    }

    /// <summary>
    /// The dock's tiers in one call: moat kept > piles touch > restow with moat > restow touching; null when nothing fits. Pure: <paramref name="aboardLines"/> are read, never moved, so this doubles as the dry run behind the fit advice.
    /// </summary>
    public static TieredPlan? PlanTiered(
        IReadOnlyList<ShipBay> bays,
        IReadOnlyList<CargoLine> aboardLines,
        LocationId destination,
        IReadOnlyList<StowBox> boxes,
        IReadOnlySet<HoldCell>? reserved,
        IReadOnlyList<LocationId> dropOrder)
    {
        ArgumentNullException.ThrowIfNull(bays);
        ArgumentNullException.ThrowIfNull(aboardLines);
        ArgumentNullException.ThrowIfNull(boxes);
        ArgumentNullException.ThrowIfNull(dropOrder);

        var placed = aboardLines
            .SelectMany(l => l.Containers)
            .Where(c => c.Placement is not null)
            .Select(c => new PlacedBox(c.Line.DropOffLocationId, c.Size, c.Placement!.Value))
            .ToList();

        foreach (var tight in new[] { false, true })
        {
            var plan = PlanBoxes(bays, placed, boxes, destination, reserved, dropOrder, tight);
            if (plan is not null)
            {
                return new TieredPlan(tight ? StowTier.Tight : StowTier.Planned, plan);
            }
        }

        var all = aboardLines.Select(ToBoxLine).Append(new BoxLine(destination, boxes.Count == 0 ? Guid.Empty : boxes[0].Id.Value, boxes)).ToList();
        var repack = RepackBoxes(bays, all, dropOrder, allowTouching: false);
        if (repack is not null)
        {
            return new TieredPlan(StowTier.Restowed, repack);
        }

        repack = RepackBoxes(bays, all, dropOrder, allowTouching: true);
        return repack is null ? null : new TieredPlan(StowTier.RestowedTight, repack);
    }

    /// <summary>
    /// Global restow: every box of every line is re-placed from an empty hold, lines in drop order so earlier drops end up nearest the ramp. Used only when the incremental plan fails; null when the hold cannot take it all even then.
    /// </summary>
    public static IReadOnlyDictionary<CargoContainerId, Placement>? Repack(
        IReadOnlyList<ShipBay> bays,
        IReadOnlyList<CargoLine> aboardLines,
        IReadOnlyList<LocationId> dropOrder,
        bool allowTouching = false)
    {
        ArgumentNullException.ThrowIfNull(bays);
        ArgumentNullException.ThrowIfNull(aboardLines);
        ArgumentNullException.ThrowIfNull(dropOrder);

        return RepackBoxes(bays, [.. aboardLines.Select(ToBoxLine)], dropOrder, allowTouching);
    }

    private static Dictionary<CargoContainerId, Placement>? RepackBoxes(
        IReadOnlyList<ShipBay> bays,
        IReadOnlyList<BoxLine> lines,
        IReadOnlyList<LocationId> dropOrder,
        bool allowTouching)
    {
        var position = dropOrder.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var placed = new List<PlacedBox>();
        var proposal = new Dictionary<CargoContainerId, Placement>();

        foreach (var line in lines.OrderBy(l => position.GetValueOrDefault(l.Destination, int.MaxValue)).ThenBy(l => l.Order))
        {
            var plan = PlanBoxes(bays, placed, line.Boxes, line.Destination, reserved: null, dropOrder, allowTouching);
            if (plan is null)
            {
                return null;
            }

            foreach (var box in line.Boxes)
            {
                proposal[box.Id] = plan[box.Id];
                placed.Add(new PlacedBox(line.Destination, box.Size, plan[box.Id]));
            }
        }

        return proposal;
    }

    private sealed record PlacedBox(LocationId Destination, ContainerSize Size, Placement Placement);

    private sealed record BoxLine(LocationId Destination, Guid Order, IReadOnlyList<StowBox> Boxes);

    private static StowBox ToBox(CargoContainer container) => new(container.Id, container.Size);

    private static BoxLine ToBoxLine(CargoLine line) => new(line.DropOffLocationId, line.Id.Value, [.. line.Containers.Select(ToBox)]);

    private static Dictionary<CargoContainerId, Placement>? PlanBoxes(
        IReadOnlyList<ShipBay> bays,
        IReadOnlyList<PlacedBox> placed,
        IEnumerable<StowBox> boxes,
        LocationId destination,
        IReadOnlySet<HoldCell>? reserved,
        IReadOnlyList<LocationId>? dropOrder,
        bool allowTouching)
    {
        var occupied = new Dictionary<HoldCell, LocationId>();
        foreach (var box in placed)
        {
            foreach (var cell in Cells(box.Size, box.Placement))
            {
                occupied[cell] = box.Destination;
            }
        }

        var earlierFront = EarlierDropsFront(placed, destination, dropOrder);
        var proposal = new Dictionary<CargoContainerId, Placement>();

        foreach (var box in boxes
            .OrderByDescending(c => c.Size.Value)
            .ThenBy(c => c.Id.Value))
        {
            var placement = FindBest(bays, occupied, destination, box.Size, reserved, earlierFront, allowTouching);
            if (placement is null)
            {
                return null;
            }

            proposal[box.Id] = placement.Value;
            foreach (var cell in Cells(box.Size, placement.Value))
            {
                occupied[cell] = destination;
            }
        }

        return proposal;
    }

    private static Placement? FindBest(
        IReadOnlyList<ShipBay> bays,
        Dictionary<HoldCell, LocationId> occupied,
        LocationId destination,
        ContainerSize size,
        IReadOnlySet<HoldCell>? reserved,
        Dictionary<(ShipBayId BayId, int Lane), int> earlierFront,
        bool allowTouching)
    {
        Placement? best = null;
        var bestKey = (Buries: int.MaxValue, Outside: int.MaxValue, Category: int.MaxValue, Bay: 0, X: 0, Y: 0, Z: 0, Rotated: 0);

        for (int bayIndex = 0; bayIndex < bays.Count; bayIndex++)
        {
            var bay = bays[bayIndex];
            foreach (var rotated in Orientations(size))
            {
                var (length, width) = Footprint(size, rotated);
                if (length > bay.Length || width > bay.Width || size.Height > bay.Height)
                {
                    continue;
                }

                for (int z = 0; z <= bay.Height - size.Height; z++)
                {
                    for (int x = 0; x <= bay.Length - length; x++)
                    {
                        for (int y = 0; y <= bay.Width - width; y++)
                        {
                            var candidate = new Placement(bay.Id, x, y, z, rotated);
                            if (!IsValid(bay, occupied, destination, size, candidate, allowTouching))
                            {
                                continue;
                            }

                            // Never sit in front of cargo that leaves the ship earlier, when there is any other spot.
                            var buries = Enumerable.Range(y, width)
                                .Any(lane => earlierFront.TryGetValue((bay.Id, lane), out var front) && front > x + length - 1) ? 1 : 0;
                            // ponytail: regions are a cell budget, not box shapes (sizes are unknown until pickup), so "fewest cells outside" pulls an oversized box to the region's deep edge instead of rejecting it.
                            var outside = reserved is null ? 0 : Cells(size, candidate).Count(c => !reserved.Contains(c));
                            var category = TouchesOwnPile(occupied, destination, size, candidate) ? 0 : 1;
                            var key = (buries, outside, category, bayIndex, x, y, z, rotated ? 1 : 0);
                            if (key.CompareTo(bestKey) < 0)
                            {
                                best = candidate;
                                bestKey = key;
                            }
                        }
                    }
                }
            }
        }
        return best;
    }

    private static bool IsValid(
        ShipBay bay,
        Dictionary<HoldCell, LocationId> occupied,
        LocationId destination,
        ContainerSize size,
        Placement candidate,
        bool allowTouching)
    {
        foreach (var cell in Cells(size, candidate))
        {
            if (occupied.ContainsKey(cell))
            {
                return false;
            }
        }

        var (length, width) = Footprint(size, candidate.Rotated);

        // Support: floor, or full same-destination support underneath.
        if (candidate.Z > 0)
        {
            for (int dx = 0; dx < length; dx++)
            {
                for (int dy = 0; dy < width; dy++)
                {
                    if (!occupied.TryGetValue(new HoldCell(bay.Id, candidate.X + dx, candidate.Y + dy, candidate.Z - 1), out var below) || below != destination)
                    {
                        return false;
                    }
                }
            }
        }

        // Clearance: expanded footprint(+- in X/Y), every level - no foreign boxes. Tight stow (hold would otherwise refuse the load) lets piles touch side by side; stacking across destinations stays forbidden via the support rule.
        if (allowTouching)
        {
            return true;
        }

        for (int x = candidate.X - 1; x <= candidate.X + length; x++)
        {
            for (var y = candidate.Y - 1; y <= candidate.Y + width; y++)
            {
                for (int z = 0; z < bay.Height; z++)
                {
                    if (occupied.TryGetValue(new HoldCell(bay.Id, x, y, z), out var other) && other != destination)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Per bay and lane, the nearest-to-ramp X of cargo aboard for drops earlier than this line's. Bays are independent: the walkway reaches every bay, so forward-bay cargo never buries aft-bay cargo. Empty when the drop order is unknown.
    /// </summary>
    private static Dictionary<(ShipBayId BayId, int Lane), int> EarlierDropsFront(
        IReadOnlyList<PlacedBox> placed,
        LocationId destination,
        IReadOnlyList<LocationId>? dropOrder)
    {
        var position = dropOrder?.ToList().IndexOf(destination) ?? -1;
        if (position < 0)
        {
            return [];
        }

        var earlier = dropOrder!.Take(position).ToHashSet();
        return placed
            .Where(b => earlier.Contains(b.Destination))
            .SelectMany(b => Cells(b.Size, b.Placement))
            .GroupBy(cell => (cell.BayId, cell.Y))
            .ToDictionary(g => g.Key, g => g.Min(cell => cell.X));
    }

    private static bool TouchesOwnPile(
        Dictionary<HoldCell, LocationId> occupied,
        LocationId destination,
        ContainerSize size,
        Placement candidate)
    {
        foreach (var (bayId, x, y, z) in Cells(size, candidate))
        {
            foreach (var (nx, ny, nz) in (ReadOnlySpan<(int, int, int)>)[(x - 1, y, z), (x + 1, y, z), (x, y - 1, z), (x, y + 1, z), (x, y, z - 1), (x, y, z + 1)])
            {
                if (occupied.TryGetValue(new HoldCell(bayId, nx, ny, nz), out var d) && d == destination)
                {
                    return true;
                }
            }
        }
        return false;
    }

    internal static IEnumerable<HoldCell> Cells(ContainerSize size, Placement placement)
    {
        var (length, width) = Footprint(size, placement.Rotated);
        for (int dx = 0; dx < length; dx++)
        {
            for (int dy = 0; dy < width; dy++)
            {
                for (int dz = 0; dz < size.Height; dz++)
                {
                    yield return new HoldCell(placement.BayId, placement.X + dx, placement.Y + dy, placement.Z + dz);
                }
            }
        }
    }

    private static (int Length, int Width) Footprint(ContainerSize size, bool rotated) => rotated ? (size.Width, size.Length) : (size.Length, size.Width);

    private static IEnumerable<bool> Orientations(ContainerSize size) => size.Length == size.Width ? [false] : [false, true];
}