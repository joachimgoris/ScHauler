using ScHauler.Models;
using ScHauler.Services;

namespace ScHauler.Tests;

[TestFixture]
public class StowageForecasterTests
{
    private static readonly Location Pickup = TestLocations.Site("Everus Harbor");

    private static Ship OneBayShip(int length = 6, int width = 4, int height = 2)
    {
        var ship = Ship.Create("Testclad", 2160);
        ship.AddBay("Bay A", length, width, height, 0, 0);
        return ship;
    }

    private static CargoLine PendingLine(Location dropOff, int scu)
    {
        var contract = Contract.Create("C", 0, DateTimeOffset.UnixEpoch);
        return contract.AddLine("Waste", new Scu(scu), Pickup, dropOff);
    }

    private static CargoLine PickedUpLine(Location dropOff, params ContainerSize[] sizes)
    {
        var line = PendingLine(dropOff, sizes.Sum(s => s.Value));
        line.PickUp(sizes);
        return line;
    }

    private static IReadOnlyList<(int X, int Y, int Z)> Coords(IReadOnlySet<HoldCell> region) =>
        [.. region.Select(c => (c.X, c.Y, c.Z)).OrderBy(c => c.X).ThenBy(c => c.Y).ThenBy(c => c.Z)];

    [Test]
    public void LastDrop_GetsTheDeepestCell()
    {
        var ship = OneBayShip();
        var first = TestLocations.Site("Lorville");
        var last = TestLocations.Site("Baijini Point");

        var forecast = StowageForecaster.Forecast([.. ship.Bays], [], [PendingLine(first, 1), PendingLine(last, 1)], [first.Id, last.Id]);

        Assert.That(forecast.Regions[last.Id].Single(), Is.EqualTo(new HoldCell(ship.Bays.First().Id, 5, 0, 0)));
    }

    [Test]
    public void FirstDrop_SitsNearerTheRamp()
    {
        var ship = OneBayShip();
        var first = TestLocations.Site("Lorville");
        var last = TestLocations.Site("Baijini Point");

        var forecast = StowageForecaster.Forecast([.. ship.Bays], [], [PendingLine(first, 3), PendingLine(last, 3)], [first.Id, last.Id]);

        Assert.That(forecast.Regions[first.Id].Max(c => c.X), Is.LessThan(forecast.Regions[last.Id].Min(c => c.X)));
    }

    [Test]
    public void Destinations_AreSeparatedByAClearanceSlice()
    {
        var ship = OneBayShip();
        var first = TestLocations.Site("Lorville");
        var last = TestLocations.Site("Baijini Point");

        var forecast = StowageForecaster.Forecast([.. ship.Bays], [], [PendingLine(first, 1), PendingLine(last, 1)], [first.Id, last.Id]);

        // Last drop took x=5; the rest of slice 5 and all of slice 4 are skipped.
        Assert.That(forecast.Regions[first.Id].Single().X, Is.EqualTo(3));
    }

    [Test]
    public void Region_FillsTowersBeforeSpreading()
    {
        var ship = OneBayShip(height: 2);
        var dest = TestLocations.Site("Lorville");

        var forecast = StowageForecaster.Forecast([.. ship.Bays], [], [PendingLine(dest, 5)], [dest.Id]);

        Assert.That(Coords(forecast.Regions[dest.Id]), Is.EqualTo([(5, 0, 0), (5, 0, 1), (5, 1, 0), (5, 1, 1), (5, 2, 0)]).AsCollection);
    }

    [Test]
    public void DeeperBay_IsFilledFirst()
    {
        var ship = Ship.Create("Testclad", 2160);
        ship.AddBay("Fwd", 2, 1, 1, 0, 0);
        var aft = ship.AddBay("Aft", 2, 1, 1, 3, 0);
        var dest = TestLocations.Site("Lorville");

        var forecast = StowageForecaster.Forecast([.. ship.Bays], [], [PendingLine(dest, 1)], [dest.Id]);

        Assert.That(forecast.Regions[dest.Id].Single(), Is.EqualTo(new HoldCell(aft.Id, 1, 0, 0)));
    }

    [Test]
    public void FrozenPlacements_BlockTheirWholeColumn()
    {
        var ship = OneBayShip(height: 2);
        var bay = ship.Bays.First();
        var aboard = PickedUpLine(TestLocations.Site("Area18"), ContainerSize.One);
        aboard.Containers.Single().Place(new Placement(bay.Id, 5, 0, 0, Rotated: false));
        var dest = TestLocations.Site("Lorville");

        var forecast = StowageForecaster.Forecast([.. ship.Bays], [.. aboard.Containers], [PendingLine(dest, 1)], [dest.Id]);

        // Not (5,0,1) either: a ghost never floats on a foreign box.
        Assert.That(forecast.Regions[dest.Id].Single(), Is.EqualTo(new HoldCell(bay.Id, 5, 1, 0)));
    }

    [Test]
    public void Region_StaysOneClearanceSliceShyOfLaterDropsCargo()
    {
        var ship = OneBayShip(length: 10, width: 4, height: 2);
        var bay = ship.Bays.First();
        var first = TestLocations.Site("Area18");
        var last = TestLocations.Site("Baijini Point");
        var aboard = PickedUpLine(last, ContainerSize.One);
        aboard.Containers.Single().Place(new Placement(bay.Id, 8, 0, 0, Rotated: false));

        var forecast = StowageForecaster.Forecast([.. ship.Bays], [.. aboard.Containers], [aboard, PendingLine(first, 1)], [first.Id, last.Id]);

        Assert.That(forecast.Regions[first.Id].Single(), Is.EqualTo(new HoldCell(bay.Id, 6, 0, 0)));
    }

    [Test]
    public void Frontier_IsPerBay_LaterCargoInTheForwardBayDoesNotCapTheAftBay()
    {
        var ship = Ship.Create("Testclad", 2160);
        var fwd = ship.AddBay("Fwd", 20, 1, 1, 0, 0);
        var aft = ship.AddBay("Aft", 10, 1, 1, 21, 0);
        var first = TestLocations.Site("Area18");
        var last = TestLocations.Site("Orison");
        var aboard = PickedUpLine(last, ContainerSize.One);
        aboard.Containers.Single().Place(new Placement(fwd.Id, 5, 0, 0, Rotated: false));

        var forecast = StowageForecaster.Forecast([.. ship.Bays], [.. aboard.Containers], [aboard, PendingLine(first, 1)], [first.Id, last.Id]);

        Assert.That(forecast.Regions[first.Id].Single(), Is.EqualTo(new HoldCell(aft.Id, 9, 0, 0)));
    }

    [Test]
    public void Frontier_IsIgnored_WhenLaterDropsCargoFillsTheFrontmostRow()
    {
        // Orison (last) already sits in the frontmost row (depth 0, nearest the ramp; the ramp itself is never blocked): nothing can be in front of it, so the earlier drop still gets the deepest free cell.
        var ship = OneBayShip(length: 10, width: 4, height: 2);
        var bay = ship.Bays.First();
        var first = TestLocations.Site("Area18");
        var last = TestLocations.Site("Orison");
        var aboard = PickedUpLine(last, ContainerSize.One);
        aboard.Containers.Single().Place(new Placement(bay.Id, 0, 0, 0, Rotated: false));

        var forecast = StowageForecaster.Forecast([.. ship.Bays], [.. aboard.Containers], [aboard, PendingLine(first, 1)], [first.Id, last.Id]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(forecast.Regions[first.Id].Single(), Is.EqualTo(new HoldCell(bay.Id, 9, 0, 0)));
            Assert.That(forecast.UnreservedScu, Is.Zero);
        }
    }

    [Test]
    public void PickedUpLines_GetNoRegion()
    {
        var ship = OneBayShip();
        var dest = TestLocations.Site("Lorville");
        var aboard = PickedUpLine(dest, ContainerSize.One);

        var forecast = StowageForecaster.Forecast([.. ship.Bays], [.. aboard.Containers], [aboard], [dest.Id]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(forecast.Regions, Is.Empty);
            Assert.That(forecast.UnreservedScu, Is.Zero);
        }
    }

    [Test]
    public void Overflow_ReportsUnreservedScu()
    {
        var ship = OneBayShip(length: 1, width: 1, height: 1);
        var dest = TestLocations.Site("Lorville");

        var forecast = StowageForecaster.Forecast([.. ship.Bays], [], [PendingLine(dest, 3)], [dest.Id]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(forecast.Regions[dest.Id], Has.Count.EqualTo(1));
            Assert.That(forecast.UnreservedScu, Is.EqualTo(2));
        }
    }

    [Test]
    public void DestinationMissingFromDropOrder_CountsAsUnreserved()
    {
        var ship = OneBayShip();
        var dest = TestLocations.Site("Lorville");

        var forecast = StowageForecaster.Forecast([.. ship.Bays], [], [PendingLine(dest, 2)], []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(forecast.Regions, Is.Empty);
            Assert.That(forecast.UnreservedScu, Is.EqualTo(2));
        }
    }

    [Test]
    public void Forecast_IsDeterministic()
    {
        var ship = OneBayShip();
        var first = TestLocations.Site("Lorville");
        var last = TestLocations.Site("Baijini Point");
        var lines = new[] { PendingLine(first, 7), PendingLine(last, 4) };

        var a = StowageForecaster.Forecast([.. ship.Bays], [], lines, [first.Id, last.Id]);
        var b = StowageForecaster.Forecast([.. ship.Bays], [], lines, [first.Id, last.Id]);

        Assert.That(Coords(a.Regions[first.Id]), Is.EqualTo(Coords(b.Regions[first.Id])).AsCollection);
    }
}
