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

        // Bodies: planets and moons alike sit under their system (Location.Create allows no deeper nesting).
        new("Hurston", LocationKind.Body, "Stanton"),
        new("Crusader", LocationKind.Body, "Stanton"),
        new("ArcCorp", LocationKind.Body, "Stanton"),
        new("microTech", LocationKind.Body, "Stanton"),
        new("Aberdeen", LocationKind.Body, "Stanton"),
        new("Arial", LocationKind.Body, "Stanton"),
        new("Ita", LocationKind.Body, "Stanton"),
        new("Magda", LocationKind.Body, "Stanton"),
        new("Cellin", LocationKind.Body, "Stanton"),
        new("Daymar", LocationKind.Body, "Stanton"),
        new("Yela", LocationKind.Body, "Stanton"),
        new("Lyria", LocationKind.Body, "Stanton"),
        new("Wala", LocationKind.Body, "Stanton"),
        new("Calliope", LocationKind.Body, "Stanton"),
        new("Clio", LocationKind.Body, "Stanton"),
        new("Euterpe", LocationKind.Body, "Stanton"),
        new("Pyro I", LocationKind.Body, "Pyro"),
        new("Monox", LocationKind.Body, "Pyro"),
        new("Bloom", LocationKind.Body, "Pyro"),
        new("Pyro IV", LocationKind.Body, "Pyro"),
        new("Pyro V", LocationKind.Body, "Pyro"),
        new("Terminus", LocationKind.Body, "Pyro"),
        new("Ignis", LocationKind.Body, "Pyro"),
        new("Vatra", LocationKind.Body, "Pyro"),
        new("Adir", LocationKind.Body, "Pyro"),
        new("Fairo", LocationKind.Body, "Pyro"),
        new("Fuego", LocationKind.Body, "Pyro"),
        new("Vuur", LocationKind.Body, "Pyro"),

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
        new("Sakura Sun Magnolia Workcenter", LocationKind.Site, "microTech"),
        new("Sakura Sun Goldenrod Workcenter", LocationKind.Site, "Hurston"),
        new("Greycat Stanton I Production Complex-B", LocationKind.Site, "Hurston"),
        new("Dupree Industrial Manufacturing Facility", LocationKind.Site, "Hurston"),
        new("microTech Logistics Depot S4LD01", LocationKind.Site, "microTech"),
        new("microTech Logistics Depot S4LD13", LocationKind.Site, "microTech"),
        new("Shubin Mining Facility SMCa-6", LocationKind.Site, "Calliope"),
        new("Shubin Mining Facility SMCa-8", LocationKind.Site, "Calliope"),

        // Outposts, mining and research sites
        new("HDMS-Oparei", LocationKind.Site, "Hurston"),
        new("HDMS-Edmond", LocationKind.Site, "Hurston"),
        new("HDMS-Stanhope", LocationKind.Site, "Hurston"),
        new("HDMS-Hadley", LocationKind.Site, "Hurston"),
        new("HDMS-Pinewood", LocationKind.Site, "Hurston"),
        new("HDMS-Thedus", LocationKind.Site, "Hurston"),
        new("Reclamation & Disposal Orinth", LocationKind.Site, "Hurston"),
        new("HDMS-Anderson", LocationKind.Site, "Aberdeen"),
        new("HDMS-Norgaard", LocationKind.Site, "Aberdeen"),
        new("HDMS-Bezdek", LocationKind.Site, "Arial"),
        new("HDMS-Lathan", LocationKind.Site, "Arial"),
        new("HDMS-Ryder", LocationKind.Site, "Ita"),
        new("HDMS-Woodruff", LocationKind.Site, "Ita"),
        new("HDMS-Hahn", LocationKind.Site, "Magda"),
        new("HDMS-Perlman", LocationKind.Site, "Magda"),
        new("Gallete Family Farms", LocationKind.Site, "Cellin"),
        new("Hickes Research Outpost", LocationKind.Site, "Cellin"),
        new("Terra Mills HydroFarm", LocationKind.Site, "Cellin"),
        new("Tram & Myers Mining", LocationKind.Site, "Cellin"),
        new("ArcCorp Mining Area 141", LocationKind.Site, "Daymar"),
        new("Shubin Mining Facility SCD-1", LocationKind.Site, "Daymar"),
        new("Kudre Ore", LocationKind.Site, "Daymar"),
        new("Bountiful Harvest Hydroponics", LocationKind.Site, "Daymar"),
        new("Eager Flats Aid Shelter", LocationKind.Site, "Daymar"),
        new("Brio's Breaker Yard", LocationKind.Site, "Daymar"),
        new("ArcCorp Mining Area 157", LocationKind.Site, "Yela"),
        new("Benson Mining Outpost", LocationKind.Site, "Yela"),
        new("Deakins Research Outpost", LocationKind.Site, "Yela"),
        new("Jumptown", LocationKind.Site, "Yela"),
        new("Shubin Mining Facility SAL-2", LocationKind.Site, "Lyria"),
        new("Shubin Mining Facility SAL-5", LocationKind.Site, "Lyria"),
        new("Humboldt Mines", LocationKind.Site, "Lyria"),
        new("Loveridge Mineral Reserve", LocationKind.Site, "Lyria"),
        new("The Orphanage", LocationKind.Site, "Lyria"),
        new("Paradise Cove", LocationKind.Site, "Lyria"),
        new("ArcCorp Mining Area 045", LocationKind.Site, "Wala"),
        new("ArcCorp Mining Area 048", LocationKind.Site, "Wala"),
        new("ArcCorp Mining Area 056", LocationKind.Site, "Wala"),
        new("ArcCorp Mining Area 061", LocationKind.Site, "Wala"),
        new("Samson & Son's Salvage Center", LocationKind.Site, "Wala"),
        new("Shady Glen Farms", LocationKind.Site, "Wala"),
        new("Rayari Deltana Research Outpost", LocationKind.Site, "microTech"),
        new("Shubin Mining Facility SM0-10", LocationKind.Site, "microTech"),
        new("Shubin Mining Facility SM0-13", LocationKind.Site, "microTech"),
        new("Shubin Mining Facility SM0-18", LocationKind.Site, "microTech"),
        new("Shubin Mining Facility SM0-22", LocationKind.Site, "microTech"),
        new("Rayari Anvik Research Outpost", LocationKind.Site, "Calliope"),
        new("Rayari Kaltag Research Outpost", LocationKind.Site, "Calliope"),
        new("Rayari Cantwell Research Outpost", LocationKind.Site, "Clio"),
        new("Rayari McGrath Research Outpost", LocationKind.Site, "Clio"),
        new("Bud's Growery", LocationKind.Site, "Euterpe"),
        new("Devlin Scrap & Salvage", LocationKind.Site, "Euterpe"),

        // Pyro stations, under the planet whose orbit or Lagrange point they sit in (wiki PYR codes); surface outposts are added from the contract page as they come up.
        new("Ruin Station", LocationKind.Site, "Terminus"),
        new("Endgame", LocationKind.Site, "Terminus"),
        new("Megumi Refueling", LocationKind.Site, "Terminus"),
        new("Dudley & Daughters", LocationKind.Site, "Terminus"),
        new("Gaslight", LocationKind.Site, "Pyro V"),
        new("Rat's Nest", LocationKind.Site, "Pyro V"),
        new("Rod's Fuel 'N Supplies", LocationKind.Site, "Pyro V"),
        new("Orbituary", LocationKind.Site, "Bloom"),
        new("Patch City", LocationKind.Site, "Bloom"),
        new("Starlight Service Station", LocationKind.Site, "Bloom"),
        new("Checkmate Station", LocationKind.Site, "Monox"),
        new("Stanton Gateway Station", LocationKind.Site, "Pyro"),
        new("Pyro Gateway Station", LocationKind.Site, "Stanton"),
    ];

    public static async Task EnsureSeededAsync(HaulerDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        // Additive: names already present (seeded earlier, or typed on the contract page) are kept as they are, missing ones are added.
        var byName = await db.Locations.ToDictionaryAsync(l => l.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var spec in Specs.Where(spec => !byName.ContainsKey(spec.Name)))
        {
            var location = Location.Create(spec.Name, spec.Kind, spec.Parent is null ? null : byName[spec.Parent]);
            byName[spec.Name] = location;
            db.Locations.Add(location);
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
