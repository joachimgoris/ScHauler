using ScHauler.Models;
using ScHauler.Services;

namespace ScHauler.Tests;

[TestFixture]
public class StopOrderingTests
{
    private static CargoLine DeliverLine(Location dropOff, params ContainerSize[] sizes)
    {
        var owner = Contract.Create("C", 0, DateTimeOffset.UnixEpoch);
        var line = owner.AddLine("Waste", new Scu(sizes.Sum(s => s.Value)), TestLocations.Site("Origin Depot"), dropOff);
        line.PickUp(sizes);
        return line;
    }

    [Test]
    public void WithCurrentLocation_NearestStopsComeFirst()
    {
        var current = TestLocations.SiteOn("Everus Harbor", TestLocations.Hurston);
        var lorville = TestLocations.SiteOn("Lorville", TestLocations.Hurston);
        var baijini = TestLocations.SiteOn("Baijini Point", TestLocations.ArcCorp);
        var ruin = TestLocations.SiteOn("Ruin Station", TestLocations.Pyro);
        var net = RouteNet.From([TestLocations.Stanton, TestLocations.Pyro, TestLocations.Hurston, TestLocations.ArcCorp, current, lorville, baijini, ruin]);
        var lines = new[]
        {
            DeliverLine(ruin, ContainerSize.Eight),
            DeliverLine(baijini, ContainerSize.Eight),
            DeliverLine(lorville, ContainerSize.Eight),
        };

        var stops = ManifestPlanner.BuildStops(lines, net, current.Id, onboardScu: 24, capacityScu: 2160);

        Assert.That(stops.Select(s => s.LocationName), Is.EqualTo(["Lorville", "Baijini Point", "Ruin Station"]).AsCollection);
    }

    [Test]
    public void WithoutCurrentLocation_StaysAlphabetical()
    {
        var lorville = TestLocations.SiteOn("Lorville", TestLocations.Hurston);
        var baijini = TestLocations.SiteOn("Baijini Point", TestLocations.ArcCorp);
        var net = RouteNet.From([TestLocations.Stanton, TestLocations.Hurston, TestLocations.ArcCorp, lorville, baijini]);
        var lines = new[] { DeliverLine(lorville, ContainerSize.Eight), DeliverLine(baijini, ContainerSize.Eight) };

        var stops = ManifestPlanner.BuildStops(lines, net, currentLocationId: null, onboardScu: 16, capacityScu: 2160);

        Assert.That(stops.Select(s => s.LocationName), Is.EqualTo(["Baijini Point", "Lorville"]).AsCollection);
    }

    [Test]
    public void NearCapacity_TiebreakPrefersTheBiggestDelivery()
    {
        var current = TestLocations.SiteOn("Everus Harbor", TestLocations.Hurston);
        var alpha = TestLocations.SiteOn("Alpha Yard", TestLocations.Hurston);
        var bravo = TestLocations.SiteOn("Bravo Yard", TestLocations.Hurston);
        var net = RouteNet.From([TestLocations.Stanton, TestLocations.Hurston, current, alpha, bravo]);
        var lines = new[]
        {
            DeliverLine(alpha, ContainerSize.Eight),
            DeliverLine(bravo, ContainerSize.ThirtyTwo),
        };

        // 40 aboard of 48 = 83%. Near capacity: Bravo frees 32, beats alpha.
        var stops = ManifestPlanner.BuildStops(lines, net, current.Id, onboardScu: 40, capacityScu: 48);

        Assert.That(stops.Select(s => s.LocationName), Is.EqualTo(["Bravo Yard", "Alpha Yard",]).AsCollection);
    }

    [Test]
    public void BelowCapacityThreshold_TiebreakIsAlphabetical()
    {
        var current = TestLocations.SiteOn("Everus Harbor", TestLocations.Hurston);
        var alpha = TestLocations.SiteOn("Alpha Yard", TestLocations.Hurston);
        var bravo = TestLocations.SiteOn("Bravo Yard", TestLocations.Hurston);
        var net = RouteNet.From([TestLocations.Stanton, TestLocations.Hurston, current, alpha, bravo]);
        var lines = new[]
        {
            DeliverLine(alpha, ContainerSize.Eight),
            DeliverLine(bravo, ContainerSize.ThirtyTwo),
        };

        var stops = ManifestPlanner.BuildStops(lines, net, current.Id, onboardScu: 40, capacityScu: 2160);

        Assert.That(stops.Select(s => s.LocationName), Is.EqualTo(["Alpha Yard", "Bravo Yard"]).AsCollection);
    }
}