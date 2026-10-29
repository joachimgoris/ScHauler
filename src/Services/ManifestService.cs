using Microsoft.EntityFrameworkCore;
using ScHauler.Data;
using ScHauler.Models;

namespace ScHauler.Services;

public sealed record ManifestSnapshot(
    IReadOnlyList<LocationStop> Stops,
    int OnboardScu,
    int CapacityScu,
    HoldView Hold,
    LocationId? CurrentLocationId,
    IReadOnlyList<Location> Locations,
    IReadOnlyList<Destination> Destinations,
    int UnreservedScu,
    bool DropOrderPinned);

public enum PickUpOutcome
{
    /// <summary>Boxes placed without touching what was aboard.</summary>
    Planned,

    /// <summary>The incremental plan failed; the whole hold was re-planned and <see cref="PickUpResult.MovedBoxes"/> boxes already aboard changed place.</summary>
    Restowed,

    /// <summary>Not loaded: the line's SCU would exceed the ship's capacity.</summary>
    Overflow,

    /// <summary>Not loaded: the boxes do not fit in the hold even after a restow.</summary>
    DoesNotFit,

    /// <summary>Stale tap: the line was not pending.</summary>
    Ignored,
}

/// <summary><paramref name="Tight"/>: piles were allowed to touch side by side because the moat rule left no room.</summary>
public sealed record PickUpResult(PickUpOutcome Outcome, int MovedBoxes = 0, bool Tight = false);

/// <summary><paramref name="HoldKnown"/> false = no ship or no bays, nothing to say. <paramref name="Fit"/> null = no box mix fits.</summary>
public sealed record PickUpAdvice(bool HoldKnown, bool Overflow, FitAdvice? Fit);

public sealed class ManifestService(IDbContextFactory<HaulerDbContext> factory)
{
    /// <summary>
    /// The ship's pinned ▲/▼ order (empty = route order) drives the drop order and the suggested-stow ghosts.
    /// </summary>
    public async Task<ManifestSnapshot> GetManifestAsync()
    {
        await using var db = await factory.CreateDbContextAsync();

        var lines = await db.CargoLines
            .AsNoTracking()
            .Include(l => l.Contract)
            .Include(l => l.PickupLocation)
            .Include(l => l.DropOffLocation)
            .Where(l => l.Status != Models.CargoLineStatus.Delivered)
            .ToListAsync();

        var ship = await db.Ships.AsNoTracking().OrderBy(s => s.Name).FirstOrDefaultAsync();
        var bays = ship?.Bays.OrderBy(b => b.DrawOffsetX).ThenBy(b => b.DrawOffsetY).ToList() ?? [];
        var aboard = lines.Where(l => l.Status == CargoLineStatus.PickedUp).SelectMany(l => l.Containers).ToList();

        var locations = await db.Locations.AsNoTracking().ToListAsync();
        var onboardScu = ManifestPlanner.OnboardScu(lines);
        var capacityScu = ship?.CargoCapacityScu ?? 0;

        var net = RouteNet.From(locations);
        var stops = ManifestPlanner.BuildStops(lines, net, ship?.CurrentLocationId, onboardScu, capacityScu);
        var destinations = ManifestPlanner.DropOrder(lines, net, ship?.CurrentLocationId, onboardScu, capacityScu, Pin(ship));
        var forecast = StowageForecaster.Forecast(bays, aboard, lines, [.. destinations.Select(d => d.Id)]);
        var hold = ManifestPlanner.BuildHoldView(bays, aboard) with
        {
            Ghosts = ManifestPlanner.BuildGhosts(bays, forecast.Regions, destinations),
        };

        return new ManifestSnapshot(
            stops,
            onboardScu,
            capacityScu,
            hold,
            ship?.CurrentLocationId,
            [.. locations.Where(l => l.Kind == LocationKind.Site).OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)],
            destinations,
            forecast.UnreservedScu,
            Pin(ship) is not null);
    }

    private static IReadOnlyList<LocationId>? Pin(Ship? ship) => ship is { PinnedDropOrder.Count: > 0 } ? ship.PinnedDropOrder : null;

    public async Task PinDropOrderAsync(IReadOnlyList<LocationId> order)
    {
        await using var db = await factory.CreateDbContextAsync();
        var ship = await db.Ships.OrderBy(s => s.Name).FirstOrDefaultAsync();
        if (ship is null)
        {
            return;
        }

        ship.PinDropOrder(order);
        await db.SaveChangesAsync();
    }

    public async Task ClearDropOrderAsync()
    {
        await using var db = await factory.CreateDbContextAsync();
        var ship = await db.Ships.OrderBy(s => s.Name).FirstOrDefaultAsync();
        if (ship is null)
        {
            return;
        }

        ship.ClearDropOrder();
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Dry run of the dock's tiers for a pending line: the largest box size whose mix still fits next to what is aboard now.
    /// </summary>
    public async Task<PickUpAdvice> AdviseAsync(CargoLineId cargoLineId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var lines = await db.CargoLines
            .AsNoTracking()
            .Include(l => l.Contract)
            .Include(l => l.PickupLocation)
            .Include(l => l.DropOffLocation)
            .Where(l => l.Status != CargoLineStatus.Delivered)
            .ToListAsync();
        var line = lines.FirstOrDefault(l => l.Id == cargoLineId);
        var ship = await db.Ships.AsNoTracking().OrderBy(s => s.Name).FirstOrDefaultAsync();
        if (line is null || line.Status != CargoLineStatus.Pending || ship is null || ship.Bays.Count == 0)
        {
            return new PickUpAdvice(HoldKnown: false, Overflow: false, Fit: null);
        }

        var onboardAfter = ManifestPlanner.OnboardScu(lines) + line.Scu.Value;
        if (ship.CargoCapacityScu > 0 && onboardAfter > ship.CargoCapacityScu)
        {
            return new PickUpAdvice(HoldKnown: true, Overflow: true, Fit: null);
        }

        var bays = ship.Bays.OrderBy(b => b.DrawOffsetX).ThenBy(b => b.DrawOffsetY).ToList();
        var net = RouteNet.From(await db.Locations.AsNoTracking().ToListAsync());
        var destinations = ManifestPlanner.DropOrder(lines, net, line.PickupLocationId, onboardAfter, ship.CargoCapacityScu, Pin(ship));
        var aboardLines = lines.Where(l => l.Status == CargoLineStatus.PickedUp).ToList();
        var fit = StowageAdvisor.LargestBoxThatFits(bays, aboardLines, line.DropOffLocationId, line.Scu.Value, [.. destinations.Select(d => d.Id)]);
        return new PickUpAdvice(HoldKnown: true, Overflow: false, fit);
    }

    public async Task AdvanceAsync(CargoLineId cargoLineId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var line = await db.CargoLines.FindAsync(cargoLineId);

        // Delivered guard: a stale tap on an already-completed row should no-op, not throw through the circuit.
        if (line is null || line.Status != CargoLineStatus.PickedUp)
        {
            return;
        }

        line.Advance();

        var ship = await db.Ships.OrderBy(s => s.Name).FirstOrDefaultAsync();
        ship?.MoveTo(line.DropOffLocationId);

        await db.SaveChangesAsync();
    }

    public async Task CorrectAsync(CargoLineId cargoLineId, CargoLineStatus status)
    {
        await using var db = await factory.CreateDbContextAsync();
        var line = await db.CargoLines.FindAsync(cargoLineId);
        if (line is null)
        {
            return;
        }

        line.Correct(status);
        await db.SaveChangesAsync();
    }

    public async Task SetCapacityAsync(int capacityScu)
    {
        await using var db = await factory.CreateDbContextAsync();
        var ship = await db.Ships.FirstOrDefaultAsync() ?? db.Ships.Add(Ship.Create("Ironclad")).Entity;
        ship.SetCapacity(Math.Max(0, capacityScu));
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Plans the boxes with the forecast region for this line's destination as a soft preference, so the real stow follows the suggested stow when it can.
    /// </summary>
    public async Task<PickUpResult> PickUpAsync(CargoLineId cargoLineId, IReadOnlyList<ContainerSize> sizes)
    {
        await using var db = await factory.CreateDbContextAsync();
        var lines = await db.CargoLines
            .Include(l => l.Contract)
            .Include(l => l.PickupLocation)
            .Include(l => l.DropOffLocation)
            .Where(l => l.Status != CargoLineStatus.Delivered)
            .ToListAsync();
        var line = lines.FirstOrDefault(l => l.Id == cargoLineId);

        if (line is null || line.Status != CargoLineStatus.Pending)
        {
            return new PickUpResult(PickUpOutcome.Ignored);
        }

        var ship = await db.Ships.OrderBy(s => s.Name).FirstOrDefaultAsync();
        if (ship is { CargoCapacityScu: > 0 } && ManifestPlanner.OnboardScu(lines) + line.Scu.Value > ship.CargoCapacityScu)
        {
            return new PickUpResult(PickUpOutcome.Overflow);
        }

        // Move first: the forecast must use the same location and onboard SCU the manifest will show after this pickup.
        ship?.MoveTo(line.PickupLocationId);
        var bays = ship?.Bays.OrderBy(b => b.DrawOffsetX).ThenBy(b => b.DrawOffsetY).ToList() ?? [];
        var aboardLines = lines.Where(l => l.Status == CargoLineStatus.PickedUp).ToList();
        var aboard = aboardLines.SelectMany(l => l.Containers).ToList();
        var net = RouteNet.From(await db.Locations.AsNoTracking().ToListAsync());
        var onboardAfter = ManifestPlanner.OnboardScu(lines) + line.Scu.Value;
        var destinations = ManifestPlanner.DropOrder(lines, net, ship?.CurrentLocationId, onboardAfter, ship?.CargoCapacityScu ?? 0, Pin(ship));
        var forecast = StowageForecaster.Forecast(bays, aboard, lines, [.. destinations.Select(d => d.Id)]);

        line.PickUp(sizes);
        if (ship is null)
        {
            await db.SaveChangesAsync();
            return new PickUpResult(PickUpOutcome.Planned);
        }

        // Nothing saved when no tier fits; the line stays pending.
        var plan = StowagePlanner.PlanTiered(
            bays,
            aboardLines,
            line.DropOffLocationId,
            [.. line.Containers.Select(c => new StowBox(c.Id, c.Size))],
            forecast.Regions.GetValueOrDefault(line.DropOffLocationId),
            [.. destinations.Select(d => d.Id)]);
        if (plan is null)
        {
            return new PickUpResult(PickUpOutcome.DoesNotFit);
        }

        var moved = 0;
        foreach (var container in aboardLines.Append(line).SelectMany(l => l.Containers))
        {
            if (!plan.Placements.TryGetValue(container.Id, out var placement))
            {
                continue;
            }

            var before = container.Placement;
            container.Place(placement);
            moved += before is not null && before != placement ? 1 : 0;
        }

        await db.SaveChangesAsync();
        return plan.Tier switch
        {
            StowTier.Planned => new PickUpResult(PickUpOutcome.Planned),
            StowTier.Tight => new PickUpResult(PickUpOutcome.Planned, Tight: true),
            StowTier.Restowed => new PickUpResult(PickUpOutcome.Restowed, moved),
            _ => new PickUpResult(PickUpOutcome.Restowed, moved, Tight: true),
        };
    }

    public async Task SetCurrentLocationAsync(LocationId locationId)
    {
        await using var db = await factory.CreateDbContextAsync();
        var ship = await db.Ships.OrderBy(s => s.Name).FirstOrDefaultAsync();
        if (ship is null)
        {
            return;
        }

        ship.MoveTo(locationId);
        await db.SaveChangesAsync();
    }
}