using ScHauler.Models;
using ScHauler.Services;

namespace ScHauler.Tests;

[TestFixture]
public class DropOrderTests
{
    private static readonly Location Pickup = TestLocations.Site("Everus Harbor");

    private static CargoLine Line(Location dropOff, bool aboard)
    {
        var contract = Contract.Create("C", 0, DateTimeOffset.UnixEpoch);
        var line = contract.AddLine("Waste", new Scu(1), Pickup, dropOff);
        if (aboard)
        {
            line.PickUp([ContainerSize.One]);
        }

        return line;
    }

    private static RouteNet Net(params Location[] sites) =>
        RouteNet.From([TestLocations.Stanton, TestLocations.Pyro, TestLocations.Hurston, TestLocations.ArcCorp, Pickup, .. sites]);

    [Test]
    public void DropOrder_WithoutCurrentLocation_IsAlphabetical_AboardOrNot()
    {
        var zulu = TestLocations.Site("Zulu Station");
        var alpha = TestLocations.Site("Alpha Station");
        var pending = TestLocations.Site("Bravo Depot");
        var lines = new[] { Line(zulu, aboard: true), Line(pending, aboard: false), Line(alpha, aboard: true) };

        var order = ManifestPlanner.DropOrder(lines, Net(zulu, alpha, pending), currentLocationId: null, onboardScu: 2, capacityScu: 2160, manual: null);

        Assert.That(order.Select(d => d.Name), Is.EqualTo(["Alpha Station", "Bravo Depot", "Zulu Station"]).AsCollection);
    }

    [Test]
    public void DropOrder_FollowsTheRoute_ForPendingAndAboardAlike()
    {
        var near = TestLocations.SiteOn("Lorville", TestLocations.Hurston);
        var far = TestLocations.SiteOn("Ruin Station", TestLocations.Pyro);
        var lines = new[] { Line(far, aboard: true), Line(near, aboard: false) };

        var order = ManifestPlanner.DropOrder(lines, Net(near, far), Pickup.Id, onboardScu: 1, capacityScu: 2160, manual: null);

        Assert.That(order.Select(d => d.Name), Is.EqualTo(["Lorville", "Ruin Station"]).AsCollection);
    }

    [Test]
    public void DropOrder_CarriesAboardAndPendingScu()
    {
        var dest = TestLocations.Site("Lorville");
        var lines = new[] { Line(dest, aboard: true), Line(dest, aboard: false), Line(dest, aboard: false) };

        var order = ManifestPlanner.DropOrder(lines, Net(dest), null, 1, 2160, manual: null);

        Assert.That(order.Single(), Is.EqualTo(new Destination(dest.Id, "Lorville", AboardScu: 1, PendingScu: 2)));
    }

    [Test]
    public void DropOrder_KeepsManualOrder_AndAppendsNewDestinations()
    {
        var zulu = TestLocations.Site("Zulu Station");
        var alpha = TestLocations.Site("Alpha Station");
        var pending = TestLocations.Site("Bravo Depot");
        var gone = TestLocations.Site("Delivered Already");
        var lines = new[] { Line(zulu, aboard: true), Line(alpha, aboard: true), Line(pending, aboard: false) };

        var order = ManifestPlanner.DropOrder(lines, Net(zulu, alpha, pending), null, 2, 2160, manual: [zulu.Id, gone.Id, alpha.Id]);

        Assert.That(order.Select(d => d.Name), Is.EqualTo(["Zulu Station", "Alpha Station", "Bravo Depot"]).AsCollection);
    }
}
