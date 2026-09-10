using ScHauler.Models;

namespace ScHauler.Tests;

internal static class TestLocations
{
    public static readonly Location Stanton = Location.Create("Stanton", LocationKind.System, parent: null);
    public static readonly Location Pyro = Location.Create("Pyro", LocationKind.System, parent: null);
    public static readonly Location Hurston = Location.Create("Hurston", LocationKind.Body, parent: Stanton);
    public static readonly Location ArcCorp = Location.Create("ArcCorp", LocationKind.Body, Stanton);

    public static Location Site(string name) => Location.Create(name, LocationKind.Site, Hurston);
    public static Location SiteOn(string name, Location parent) => Location.Create(name, LocationKind.Site, parent);
}