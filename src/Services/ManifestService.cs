using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using ScHauler.Data;
using ScHauler.Models;

namespace ScHauler.Services;

public sealed record ManifestSnapshot(IReadOnlyList<LocationStop> Stops, int OnboardScu, int CapacityScu, HoldView Hold);

public sealed class ManifestService(IDbContextFactory<HaulerDbContext> factory)
{
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
        var aboard = lines.Where(l => l.Status == CargoLineStatus.PickedUp).SelectMany(l => l.Containers);

        return new ManifestSnapshot(
            ManifestPlanner.BuildStops(lines),
            ManifestPlanner.OnboardScu(lines),
            ship?.CargoCapacityScu ?? 0,
            ManifestPlanner.BuildHoldView(bays, aboard));
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

    public async Task PickUpAsync(CargoLineId cargoLineId, IReadOnlyList<ContainerSize> sizes)
    {
        await using var db = await factory.CreateDbContextAsync();
        var line = await db.CargoLines.FirstOrDefaultAsync(l => l.Id == cargoLineId);

        if (line is null || line.Status != CargoLineStatus.Pending)
        {
            return;
        }

        line.PickUp(sizes);

        var ship = await db.Ships.OrderBy(s => s.Name).FirstOrDefaultAsync();
        if (ship is not null)
        {
            var bays = ship.Bays.OrderBy(b => b.DrawOffsetX).ThenBy(b => b.DrawOffsetY).ToList();
            var aboardOthers = await db.CargoLines.Where(l => l.Status == CargoLineStatus.PickedUp && l.Id != cargoLineId).ToListAsync();

            var plan = StowagePlanner.Plan(bays, [.. aboardOthers.SelectMany(l => l.Containers)], line);
            if (plan is not null)
            {
                foreach (var container in line.Containers)
                {
                    container.Place(plan[container.Id]);
                }
            }
            // plan == null: hold too full or fragmented - the boxes stay unplaced and the hold view shows a note.
        }

        await db.SaveChangesAsync();
    }
}