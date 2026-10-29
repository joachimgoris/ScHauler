using ScHauler.Models;

namespace ScHauler.Services;

public sealed record StowForecast(IReadOnlyDictionary<LocationId, IReadOnlySet<HoldCell>> Regions, int UnreservedScu);

/// <summary>
/// Pure suggested-stow forecast: reserves cells per destination for Pending lines (1 SCU = 1 cell), last drop deepest, each region contiguous in walk order, frontier never backfills. Frozen placements block their whole column. Never stored - recomputed on every manifest read.
/// Walk order: depth (DrawOffsetX + X) descending, then bay, Y, Z - towers fill before the pile spreads. A full depth slice is skipped between destinations so the planner's "never touch" rule stays satisfiable.
/// </summary>
public static class StowageForecaster
{
    public static StowForecast Forecast(
        IReadOnlyList<ShipBay> bays,
        IReadOnlyList<CargoContainer> aboard,
        IEnumerable<CargoLine> lines,
        IReadOnlyList<LocationId> dropOrder)
    {
        ArgumentNullException.ThrowIfNull(bays);
        ArgumentNullException.ThrowIfNull(aboard);
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(dropOrder);

        var placed = aboard.Where(c => c.Placement is not null).ToList();
        var blockedColumns = placed
            .SelectMany(c => StowagePlanner.Cells(c.Size, c.Placement!.Value))
            .Select(c => (c.BayId, c.X, c.Y))
            .ToHashSet();

        var walk = bays
            .SelectMany((bay, bayIndex) =>
                from x in Enumerable.Range(0, bay.Length)
                from y in Enumerable.Range(0, bay.Width)
                from z in Enumerable.Range(0, bay.Height)
                where !blockedColumns.Contains((bay.Id, x, y))
                select (Depth: bay.DrawOffsetX + x, BayIndex: bayIndex, Cell: new HoldCell(bay.Id, x, y, z)))
            .OrderByDescending(c => c.Depth)
            .ThenBy(c => c.BayIndex)
            .ThenBy(c => c.Cell.Y)
            .ThenBy(c => c.Cell.Z)
            .ToList();

        var needed = lines
            .Where(l => l.Status == CargoLineStatus.Pending)
            .GroupBy(l => l.DropOffLocationId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Scu.Value));

        var regions = new Dictionary<LocationId, IReadOnlySet<HoldCell>>();
        var unreserved = needed.Where(kv => !dropOrder.Contains(kv.Key)).Sum(kv => kv.Value);
        var index = 0;

        var order = Enumerable.Reverse(dropOrder).Distinct().ToList();
        foreach (var (destination, position) in order.Select((d, i) => (d, i)))
        {
            if (!needed.TryGetValue(destination, out var scu) || scu == 0)
            {
                continue;
            }

            // Frontier, per bay (the walkway reaches every bay): never deeper than one clearance slice short of cargo already aboard for a later drop in that bay.
            var laterDrops = order.Take(position).ToHashSet();
            var caps = placed
                .Where(c => laterDrops.Contains(c.Line.DropOffLocationId))
                .GroupBy(c => c.Placement!.Value.BayId)
                .ToDictionary(g => g.Key, g => g.Min(c => c.Placement!.Value.X) - 2);
            bool Allowed(HoldCell cell) => !caps.TryGetValue(cell.BayId, out var cap) || cell.X <= cap;

            // Caps that leave nothing (later cargo already in every frontmost row) are ignored; the stow check reports that case.
            if (!walk.Skip(index).Any(w => Allowed(w.Cell)))
            {
                caps.Clear();
            }

            var region = new HashSet<HoldCell>();
            while (region.Count < scu && index < walk.Count)
            {
                var cell = walk[index++].Cell;
                if (Allowed(cell))
                {
                    region.Add(cell);
                }
            }

            regions[destination] = region;
            unreserved += scu - region.Count;
            if (region.Count == 0)
            {
                continue;
            }

            // Clearance: finish the current depth slice and skip one more.
            var lastDepth = walk[index - 1].Depth;
            while (index < walk.Count && walk[index].Depth >= lastDepth - 1)
            {
                index++;
            }
        }

        return new StowForecast(regions, unreserved);
    }
}
