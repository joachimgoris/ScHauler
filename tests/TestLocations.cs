using ScHauler.Models;

namespace ScHauler.Tests;

internal static class TestLocations
{
    private static readonly Location Stanton = Location.Create("Stanton", LocationKind.System, parent: null);
    private static readonly Location Hurston = Location.Create("Hurston", LocationKind.Body, parent: Stanton);
    public static Location Site(string name) => Location.Create(name, LocationKind.Site, Hurston);
}