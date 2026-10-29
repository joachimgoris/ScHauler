using ScHauler.Models;

namespace ScHauler.Services;

public sealed record FitAdvice(ContainerSize LargestBox, StowTier Tier);

/// <summary>
/// Pure fit advice for a pending line: the largest box size whose greedy mix (no box larger than it) still fits through the dock's tiers. Null = no mix fits.
/// ponytail: smaller boxes are assumed to fit whenever larger ones do (own-destination support makes that hold in practice, not by proof), so the search stops at the first size that fits.
/// </summary>
public static class StowageAdvisor
{
    public static FitAdvice? LargestBoxThatFits(
        IReadOnlyList<ShipBay> bays,
        IReadOnlyList<CargoLine> aboardLines,
        LocationId destination,
        int scu,
        IReadOnlyList<LocationId> dropOrder)
    {
        ArgumentNullException.ThrowIfNull(bays);
        ArgumentNullException.ThrowIfNull(aboardLines);
        ArgumentNullException.ThrowIfNull(dropOrder);

        // Cheap necessary condition first: one cell per SCU.
        var free = bays.Sum(b => b.Length * b.Width * b.Height) - aboardLines.SelectMany(l => l.Containers).Sum(c => c.Size.Value);
        if (scu <= 0 || scu > free)
        {
            return null;
        }

        foreach (var max in ContainerSize.All.Reverse())
        {
            var boxes = Split(scu, max).Select(size => new StowBox(CargoContainerId.New(), size)).ToList();
            var plan = StowagePlanner.PlanTiered(bays, aboardLines, destination, boxes, reserved: null, dropOrder);
            if (plan is not null)
            {
                return new FitAdvice(max, plan.Tier);
            }
        }

        return null;
    }

    /// <summary>
    /// Greedy split, largest box first, none larger than <paramref name="max"/>: 500 → 15 × 32 + 16 + 4.
    /// </summary>
    public static IReadOnlyList<ContainerSize> Split(int scu, ContainerSize max)
    {
        ArgumentNullException.ThrowIfNull(max);

        var boxes = new List<ContainerSize>();
        var left = scu;
        foreach (var size in ContainerSize.All.Reverse().Where(s => s.Value <= max.Value))
        {
            while (left >= size.Value)
            {
                boxes.Add(size);
                left -= size.Value;
            }
        }

        return boxes;
    }
}
