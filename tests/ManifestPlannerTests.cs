using ScHauler.Models;
using ScHauler.Services;

namespace ScHauler.Tests;

[TestFixture]
public class ManifestPlannerTests
{

    private static readonly Dictionary<string, Location> LocationCache = new(StringComparer.OrdinalIgnoreCase);

    private static Location Loc(string name) => LocationCache.TryGetValue(name, out var found) ? found : LocationCache[name] = TestLocations.Site(name);

    private static CargoLine Line(
        CargoLineStatus status,
        string pickup = "Everus Harbor",
        string dropOff = "Baijini Point",
        string commodity = "Waste",
        ContainerSize[]? sizes = null,
        string contract = "Contract A")
    {
        var boxes = sizes ?? [ContainerSize.Eight, ContainerSize.Two];
        var owner = Contract.Create(contract, 0, DateTimeOffset.UnixEpoch);
        var line = owner.AddLine(commodity, new Scu(boxes.Sum(s => s.Value)), Loc(pickup), Loc(dropOff));
        if (status is CargoLineStatus.PickedUp or CargoLineStatus.Delivered)
        {
            line.PickUp(boxes);
        }

        if (status is CargoLineStatus.Delivered)
        {
            line.Advance();
        }

        return line;
    }

    [Test]
    public void PendingLine_IsActionableAtPickupLocation_AsPickup()
    {
        var line = Line(CargoLineStatus.Pending);

        var stops = ManifestPlanner.BuildStops([line]);

        Assert.That(stops, Has.Count.EqualTo(1));
        Assert.That(stops[0].LocationName, Is.EqualTo("Everus Harbor"));
        Assert.That(stops[0].Actions, Has.Count.EqualTo(1));

        var action = stops[0].Actions[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(action.Kind, Is.EqualTo(ActionKind.Pickup));
            Assert.That(action.CargoLineId, Is.EqualTo(line.Id));
        }
    }

    [Test]
    public void PickedUpLine_IsActionableAtDropOffLocation_AsDeliver()
    {
        var stops = ManifestPlanner.BuildStops([Line(CargoLineStatus.PickedUp)]);

        Assert.That(stops, Has.Count.EqualTo(1));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stops[0].LocationName, Is.EqualTo("Baijini Point"));
            Assert.That(stops[0].Actions, Has.Count.EqualTo(1));
        }
        Assert.That(stops[0].Actions[0].Kind, Is.EqualTo(ActionKind.Deliver));
    }

    [Test]
    public void DeliveredLines_AreExcluded()
    {
        var stops = ManifestPlanner.BuildStops([Line(CargoLineStatus.Delivered)]);

        Assert.That(stops, Is.Empty);
    }

    [Test]
    public void SameLocation_DeliveriesComeBeforePickups()
    {
        var lines = new[]
        {
            Line(CargoLineStatus.PickedUp, pickup: "Lorville", dropOff: "Everus Harbor"),
            Line(CargoLineStatus.Pending, pickup: "Everus Harbor", dropOff: "Area18"),
        };

        var stop = ManifestPlanner.BuildStops(lines).Single(s => s.LocationName == "Everus Harbor");

        Assert.That(stop.Actions, Has.Count.EqualTo(2));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(stop.Actions[0].Kind, Is.EqualTo(ActionKind.Deliver));
            Assert.That(stop.Actions[1].Kind, Is.EqualTo(ActionKind.Pickup));
        }
    }

    [Test]
    public void StopTallies_SumScuPerKind()
    {
        var lines = new[]
        {
            Line(CargoLineStatus.Pending, pickup: "Lorville", sizes: [ContainerSize.Eight]),
            Line(CargoLineStatus.Pending, pickup: "Lorville", sizes: [ContainerSize.Four], contract: "Contract B"),
            Line(CargoLineStatus.PickedUp, pickup: "Area18", dropOff: "Lorville", sizes: [ContainerSize.Four, ContainerSize.Two]),
        };

        var stop = ManifestPlanner.BuildStops(lines).Single(s => s.LocationName == "Lorville");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stop.PickupScu, Is.EqualTo(12));
            Assert.That(stop.DeliverScu, Is.EqualTo(6));
        }
    }

    [Test]
    public void OnboardScu_CountsOnlyPickedUpLines()
    {
        var lines = new[]
        {
            Line(CargoLineStatus.Pending, sizes: [ContainerSize.ThirtyTwo, ContainerSize.ThirtyTwo, ContainerSize.ThirtyTwo, ContainerSize.Four]),
            Line(CargoLineStatus.PickedUp, sizes: [ContainerSize.Eight, ContainerSize.Four]),
            Line(CargoLineStatus.PickedUp, sizes: [ContainerSize.Eight]),
            Line(CargoLineStatus.Delivered, sizes: [ContainerSize.ThirtyTwo, ContainerSize.Sixteen, ContainerSize.Two]),
        };

        Assert.That(ManifestPlanner.OnboardScu(lines), Is.EqualTo(20));
    }

    [Test]
    public void Stops_AreOrderedAlphabetically()
    {
        var lines = new[]
        {
            Line(CargoLineStatus.Pending, pickup: "Orison"),
            Line(CargoLineStatus.Pending, pickup: "Area18"),
        };

        var stops = ManifestPlanner.BuildStops(lines);

        Assert.That(stops.Select(s => s.LocationName), Is.EqualTo(["Area18", "Orison"]).AsCollection);
    }

    [Test]
    public void StopAction_CarriesBoxBreakdown_LargestFirst()
    {
        var line = Line(CargoLineStatus.PickedUp, sizes: [ContainerSize.Two, ContainerSize.Eight, ContainerSize.Eight]);

        var action = ManifestPlanner.BuildStops([line])[0].Actions[0];

        Assert.That(
            action.Boxes.Select(b => (b.Size.Value, b.Count)), Is.EqualTo([(8, 2), (2, 1)]).AsCollection);
    }

    [Test]
    public void PendingAction_HasNoBoxBreakdown()
    {
        var action = ManifestPlanner.BuildStops([Line(CargoLineStatus.Pending)])[0].Actions[0];

        Assert.That(action.Boxes, Is.Empty);
    }

    [Test]
    public void HoldView_PlacesBoxesInTheirBay_WithRotationApplied()
    {
        var ship = Ship.Create("Testclad", 2160);
        ship.AddBay("Bay A", 4, 4, 2, 0, 0);
        var line = Line(CargoLineStatus.PickedUp, sizes: [ContainerSize.Two]);
        line.Containers.First().Place(new Placement(ship.Bays.First().Id, 1, 2, 0, Rotated: true));

        var hold = ManifestPlanner.BuildHoldView([.. ship.Bays], line.Containers);

        var box = hold.Bays.Single().Boxes.Single();
        Assert.That((box.X, box.Y, box.Length, box.Width), Is.EqualTo((1, 2, 1, 2)));
    }

    [Test]
    public void HoldView_CountsUnplacedBoxes()
    {
        var ship = Ship.Create("Testclad", 2160);
        ship.AddBay("Bay A", 4, 4, 2, 0, 0);
        var line = Line(CargoLineStatus.PickedUp, sizes: [ContainerSize.Eight, ContainerSize.Two]);

        var hold = ManifestPlanner.BuildHoldView([.. ship.Bays], line.Containers);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hold.UnplacedBoxes, Is.EqualTo(2));
            Assert.That(hold.Bays.Single().Boxes, Is.Empty);
        }
    }
}