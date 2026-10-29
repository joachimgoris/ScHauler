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
    int UnreservedScu);

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

public sealed class ManifestService(IDbContextFactory<HaulerDbContext> factory)
{
    /// <summary>
    /// <paramref name="dropOrder"/> is the page's manual ▲/▼ order (null = route order); it drives the suggested-stow ghosts.
    /// </summary>
    public async Task<ManifestSnapshot> GetManifestAsync(IReadOnlyList<LocationId>? dropOrder = null)
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
        var destinations = ManifestPlanner.DropOrder(lines, net, ship?.CurrentLocationId, onboardScu, capacityScu, dropOrder);
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
            forecast.UnreservedScu);
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
    public async Task<PickUpResult> PickUpAsync(CargoLineId cargoLineId, IReadOnlyList<ContainerSize> sizes, IReadOnlyList<LocationId>? dropOrder = null)
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
        var aboard = lines.Where(l => l.Status == CargoLineStatus.PickedUp).SelectMany(l => l.Containers).ToList();
        var net = RouteNet.From(await db.Locations.AsNoTracking().ToListAsync());
        var onboardAfter = ManifestPlanner.OnboardScu(lines) + line.Scu.Value;
        var destinations = ManifestPlanner.DropOrder(lines, net, ship?.CurrentLocationId, onboardAfter, ship?.CargoCapacityScu ?? 0, dropOrder);
        var forecast = StowageForecaster.Forecast(bays, aboard, lines, [.. destinations.Select(d => d.Id)]);

        line.PickUp(sizes);
        if (ship is null)
        {
            await db.SaveChangesAsync();
            return new PickUpResult(PickUpOutcome.Planned);
        }

        // Tiers: moat kept > piles touch > restow with moat > restow touching. Nothing saved when none fits; the line stays pending.
        var order = destinations.Select(d => d.Id).ToList();
        var reserved = forecast.Regions.GetValueOrDefault(line.DropOffLocationId);
        foreach (var tight in new[] { false, true })
        {
            var plan = StowagePlanner.Plan(bays, aboard, line, reserved, order, allowTouching: tight);
            if (plan is null)
            {
                continue;
            }

            foreach (var container in line.Containers)
            {
                container.Place(plan[container.Id]);
            }

            await db.SaveChangesAsync();
            return new PickUpResult(PickUpOutcome.Planned, Tight: tight);
        }

        var aboardLines = lines.Where(l => l.Status == CargoLineStatus.PickedUp).ToList();
        var repack = StowagePlanner.Repack(bays, aboardLines, order);
        var repackTight = repack is null;
        repack ??= StowagePlanner.Repack(bays, aboardLines, order, allowTouching: true);
        if (repack is null)
        {
            return new PickUpResult(PickUpOutcome.DoesNotFit);
        }

        var moved = 0;
        foreach (var container in aboardLines.SelectMany(l => l.Containers))
        {
            var before = container.Placement;
            container.Place(repack[container.Id]);
            moved += before is not null && before != container.Placement ? 1 : 0;
        }

        await db.SaveChangesAsync();
        return new PickUpResult(PickUpOutcome.Restowed, moved, repackTight);
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