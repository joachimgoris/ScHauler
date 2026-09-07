using ScHauler.Models;

namespace ScHauler.Services;

/// <summary>
/// Pure stowage planning. Rules: a box fits inside one bay (walkways are bay boundaries); no overlap; support is the floor or full SAME-destination support; different destinations never touch - the footprint expanded by one cell in X/Y must hold no foreign box at any level. Placements are proposed one at pickup and frozen; existing placements are immovable input.
/// Deterministic: bays in order, then lowest Z, X, Y; candidates touching the own-destination pile win over free-standing ones; unrotated before rotated. Returns null when the line cannot fit (atomic - no partial plans).
/// </summary>
public static class StowagePlanner
{
    public static IReadOnlyDictionary<CargoContainerId, Placement>? Plan(
        IReadOnlyList<ShipBay> bays,
        IReadOnlyList<CargoContainer> aboard,
        CargoLine pickedUpLine)
    {
        ArgumentNullException.ThrowIfNull(bays);
        ArgumentNullException.ThrowIfNull(aboard);
        ArgumentNullException.ThrowIfNull(pickedUpLine);

        var occupied = new Dictionary<(ShipBayId BayId, int X, int Y, int Z), LocationId>();
        foreach (var container in aboard)
        {
            if (container.Placement is { } placement)
            {
                foreach (var cell in Cells(container.Size, placement))
                {
                    occupied[cell] = container.Line.DropOffLocationId;
                }
            }
        }

        var destination = pickedUpLine.DropOffLocationId;
        var proposal = new Dictionary<CargoContainerId, Placement>();

        foreach (var box in pickedUpLine.Containers
            .Where(c => c.Placement is null)
            .OrderByDescending(c => c.Size.Value)
            .ThenBy(c => c.Id.Value))
        {
            var placement = FindBest(bays, occupied, destination, box.Size);
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
        Dictionary<(ShipBayId BayId, int X, int Y, int Z), LocationId> occupied,
        LocationId destination,
        ContainerSize size)
    {
        Placement? best = null;
        var bestKey = (Category: int.MaxValue, Bay: 0, Z: 0, X: 0, Y: 0, Rotated: 0);

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
                            if (!IsValid(bay, occupied, destination, size, candidate))
                            {
                                continue;
                            }

                            var category = TouchesOwnPile(occupied, destination, size, candidate) ? 0 : 1;
                            var key = (category, bayIndex, z, x, y, rotated ? 1 : 0);
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
        Dictionary<(ShipBayId BayId, int X, int Y, int Z), LocationId> occupied,
        LocationId destination,
        ContainerSize size,
        Placement candidate
    )
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
                    if (!occupied.TryGetValue((bay.Id, candidate.X + dx, candidate.Y + dy, candidate.Z - 1), out var below) || below != destination)
                    {
                        return false;
                    }
                }
            }
        }

        // Clearance: expanded footprint(+- in X/Y), every level - no foreign boxes.
        for (int x = candidate.X - 1; x <= candidate.X + length; x++)
        {
            for (var y = candidate.Y - 1; y <= candidate.Y + width; y++)
            {
                for (int z = 0; z < bay.Height; z++)
                {
                    if (occupied.TryGetValue((bay.Id, x, y, z), out var other) && other != destination)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static bool TouchesOwnPile(
        Dictionary<(ShipBayId BayId, int X, int Y, int Z), LocationId> occupied,
        LocationId destination,
        ContainerSize size,
        Placement candidate)
    {
        foreach (var (bayId, x, y, z) in Cells(size, candidate))
        {
            foreach (var (nx, ny, nz) in (ReadOnlySpan<(int, int, int)>)[(x - 1, y, z), (x + 1, y, z), (x, y - 1, z), (x, y + 1, z), (x, y, z - 1), (x, y, z + 1)])
            {
                if (occupied.TryGetValue((bayId, nx, ny, nz), out var d) && d == destination)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static IEnumerable<(ShipBayId BayId, int X, int Y, int Z)> Cells(ContainerSize size, Placement placement)
    {
        var (length, width) = Footprint(size, placement.Rotated);
        for (int dx = 0; dx < length; dx++)
        {
            for (int dy = 0; dy < width; dy++)
            {
                for (int dz = 0; dz < size.Height; dz++)
                {
                    yield return (placement.BayId, placement.X + dx, placement.Y + dy, placement.Z + dz);
                }
            }
        }
    }

    private static (int Length, int Width) Footprint(ContainerSize size, bool rotated) => rotated ? (size.Width, size.Length) : (size.Length, size.Width);

    private static IEnumerable<bool> Orientations(ContainerSize size) => size.Length == size.Width ? [false] : [false, true];
}