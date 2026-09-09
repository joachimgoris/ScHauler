using Microsoft.EntityFrameworkCore;
using ScHauler.Models;

namespace ScHauler.Data;

public static class LocationSeed
{
    private sealed record Spec(string Name, LocationKind Kind, string? Parent);

    // Order matters: parents before children.
    private static readonly Spec[] Specs =
    [
        new("Stanton", LocationKind.System, null),
        new("Pyro", LocationKind.System, null),

        new("Hurston", LocationKind.Body, "Stanton"),
        new("Crusader", LocationKind.Body, "Stanton"),
        new("ArcCorp", LocationKind.Body, "Stanton"),
        new("microTech", LocationKind.Body, "Stanton"),
        new("Yela", LocationKind.Body, "Stanton"),
        new("Cellin", LocationKind.Body, "Stanton"),
        new("Calliope", LocationKind.Body, "Stanton"),

        // Cities & main ports
        new("Area18", LocationKind.Site, "ArcCorp"),
        new("Lorville", LocationKind.Site, "Hurston"),
        new("New Babbage", LocationKind.Site, "microTech"),
        new("Orison", LocationKind.Site, "Crusader"),
        new("Everus Harbor", LocationKind.Site, "Hurston"),
        new("Baijini Point", LocationKind.Site, "ArcCorp"),
        new("Port Tressler", LocationKind.Site, "microTech"),
        new("Seraphim Station", LocationKind.Site, "Crusader"),
        new("GrimHEX", LocationKind.Site, "Yela"),

        // Stanton Lagrange stations (deep space: parent = system)
        new("ARC-L1 Wide Forest Station", LocationKind.Site, "Stanton"),
        new("ARC-L2 Lively Pathway Station", LocationKind.Site, "Stanton"),
        new("ARC-L3 Modern Express Station", LocationKind.Site, "Stanton"),
        new("ARC-L4 Faint Glen Station", LocationKind.Site, "Stanton"),
        new("ARC-L5 Yellow Core Station", LocationKind.Site, "Stanton"),
        new("CRU-L1 Ambitious Dream Station", LocationKind.Site, "Stanton"),
        new("CRU-L4 Shallow Fields Station", LocationKind.Site, "Stanton"),
        new("CRU-L5 Beautiful Glen Station", LocationKind.Site, "Stanton"),
        new("HUR-L1 Green Glade Station", LocationKind.Site, "Stanton"),
        new("HUR-L2 Faithful Dream Station", LocationKind.Site, "Stanton"),
        new("HUR-L3 Thundering Express Station", LocationKind.Site, "Stanton"),
        new("HUR-L4 Melodic Fields Station", LocationKind.Site, "Stanton"),
        new("HUR-L5 High Course Station", LocationKind.Site, "Stanton"),
        new("MIC-L1 Shallow Frontier Station", LocationKind.Site, "Stanton"),
        new("MIC-L2 Long Forest Station", LocationKind.Site, "Stanton"),
        new("MIC-L3 Endless Odyssey Station", LocationKind.Site, "Stanton"),
        new("MIC-L4 Red Crossroads Station", LocationKind.Site, "Stanton"),
        new("MIC-L5 Modern Icarus Station", LocationKind.Site, "Stanton"),

        // Distribution centers & depots
        new("Covalex Distribution Centre S1DC06", LocationKind.Site, "Hurston"),
        new("Greycat Stanton IV Production Complex-A", LocationKind.Site, "microTech"),
        new("HDPC-Cassillo", LocationKind.Site, "Hurston"),
        new("HDPC-Farnesway", LocationKind.Site, "Hurston"),
        new("Sakura Sun Magnolia Workcenter", LocationKind.Site, "Cellin"),
        new("microTech Logistics Depot S4LD01", LocationKind.Site, "microTech"),
        new("microTech Logistics Depot S4LD13", LocationKind.Site, "microTech"),
        new("Shubin Mining Facility SMCa-6", LocationKind.Site, "Calliope"),
        new("Shubin Mining Facility SMCa-8", LocationKind.Site, "Calliope"),

        // Pyro
        new("Ruin Station", LocationKind.Site, "Pyro"),
        new("Checkmate Station", LocationKind.Site, "Pyro"),
        new("Patch City", LocationKind.Site, "Pyro"),
        new("Orbituary", LocationKind.Site, "Pyro"),
        new("Gaslight", LocationKind.Site, "Pyro"),
        new("Megumi Refueling", LocationKind.Site, "Pyro"),
        new("Rod's Fuel 'N Supplies", LocationKind.Site, "Pyro"),
        new("Stanton Gateway Station", LocationKind.Site, "Pyro"),
        new("Pyro Gateway Station", LocationKind.Site, "Stanton"),
        new("Endgame", LocationKind.Site, "Pyro"),
    ];

    public static async Task EnsureSeededAsync(HaulerDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (!await db.Locations.AnyAsync())
        {
            var byName = new Dictionary<string, Location>(StringComparer.OrdinalIgnoreCase);
            foreach (var spec in Specs)
            {
                var location = Location.Create(spec.Name, spec.Kind, spec.Parent is null ? null : byName[spec.Parent]);
                byName[spec.Name] = location;
                db.Locations.Add(location);
            }
        }

        if (!await db.Ships.AnyAsync())
        {
            var ironclad = Ship.Create("Ironclad", 2160);
            ironclad.AddBay("Fwd port", length: 20, width: 6, height: 6, drawOffsetX: 0, drawOffsetY: 0);
            ironclad.AddBay("Fwd starboard", length: 20, width: 6, height: 6, drawOffsetX: 0, drawOffsetY: 7);
            ironclad.AddBay("Aft port", length: 10, width: 6, height: 6, drawOffsetX: 21, drawOffsetY: 0);
            ironclad.AddBay("Aft starboard", length: 10, width: 6, height: 6, drawOffsetX: 21, drawOffsetY: 7);
            db.Ships.Add(ironclad);
        }

        await db.SaveChangesAsync();
    }
}
