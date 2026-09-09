using ScHauler.Models;
using ScHauler.Services;

namespace ScHauler.Tests;

[TestFixture]
public class StowagePlannerTests
{
    private static Ship OneBayShip(int length = 6, int width = 4, int height = 2)
    {
        var ship = Ship.Create("Testclad", 2160);
        ship.AddBay("Bay A", length, width, height, 0, 0);
        return ship;
    }

    private static CargoLine PickedUpLine(string dropOff, params ContainerSize[] sizes)
    {
        var contract = Contract.Create("C", 0, DateTimeOffset.UnixEpoch);
        var line = contract.AddLine("Waste", new Scu(sizes.Sum(s => s.Value)), TestLocations.Site("Everus Harbor"), TestLocations.Site(dropOff));
        line.PickUp(sizes);
        return line;
    }

    private static void Apply(CargoLine line, IReadOnlyDictionary<CargoContainerId, Placement> plan)
    {
        foreach (var container in line.Containers)
        {
            container.Place(plan[container.Id]);
        }
    }

    [Test]
    public void FirstBox_GoesToTheOrigin()
    {
        var ship = OneBayShip();
        var line = PickedUpLine("Baijini Point", ContainerSize.One);

        var plan = StowagePlanner.Plan([.. ship.Bays], [.. line.Containers], line);

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.Values.Single(), Is.EqualTo(new Placement(ship.Bays.First().Id, 0, 0, 0, Rotated: false)));
    }

    [Test]
    public void SameDestination_SecondBoxHugsTheFirst()
    {
        var ship = OneBayShip();
        var line = PickedUpLine("Baijini Point", ContainerSize.One, ContainerSize.One);

        var plan = StowagePlanner.Plan([.. ship.Bays], [.. line.Containers], line);

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.Values.OrderBy(p => p.Z).Select(p => (p.X, p.Y, p.Z)),
            Is.EqualTo([(0, 0, 0), (0, 0, 1)]).AsCollection);
    }

    [Test]
    public void DifferentDestinations_NeverTouch()
    {
        var ship = OneBayShip();
        var first = PickedUpLine("Baijini Point", ContainerSize.One);
        Apply(first, StowagePlanner.Plan([.. ship.Bays], [.. first.Containers], first)!);

        var second = PickedUpLine("Area18", ContainerSize.One);
        var plan = StowagePlanner.Plan([.. ship.Bays], [.. first.Containers], second);

        Assert.That(plan, Is.Not.Null);
        var b = plan.Values.Single();
        var a = first.Containers.Single().Placement!.Value;
        using (Assert.EnterMultipleScope())
        {
            // Chebyshev distance >= 2 in the horizontal plane: at least one empty cell between piles.
            Assert.That(Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)), Is.GreaterThanOrEqualTo(2));
            Assert.That(b.Z, Is.Zero);
        }
    }

    [Test]
    public void ForeignPile_IsNeverUsedAsSupport()
    {
        // Ground floor exactly filled by destination A; destination B's box may not rest on it and has no floor left: no fit.
        var ship = OneBayShip(length: 2, width: 2, height: 2);
        var first = PickedUpLine("Baijini Point", ContainerSize.Four);
        Apply(first, StowagePlanner.Plan([.. ship.Bays], [.. first.Containers], first)!);

        var second = PickedUpLine("Area18", ContainerSize.One);

        Assert.That(StowagePlanner.Plan([.. ship.Bays], [.. first.Containers], second), Is.Null);
    }

    [Test]
    public void OwnPile_IsStackedOn_WhenTheFloorIsFull()
    {
        var ship = OneBayShip(length: 2, width: 2, height: 2);
        var line = PickedUpLine("Baijini Point", ContainerSize.Four, ContainerSize.One);

        var plan = StowagePlanner.Plan([.. ship.Bays], [.. line.Containers], line);

        Assert.That(plan, Is.Not.Null);
        var one = line.Containers.Single(c => c.Size == ContainerSize.One);
        Assert.That(plan[one.Id].Z, Is.EqualTo(1));
    }

    [Test]
    public void Box_IsRotated_WhenOnlyTheRotationFits()
    {
        var ship = OneBayShip(length: 1, width: 4, height: 1);
        var line = PickedUpLine("Baijini Point", ContainerSize.Two);

        var plan = StowagePlanner.Plan([.. ship.Bays], [.. line.Containers], line);

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.Values.Single().Rotated, Is.True);
    }

    [Test]
    public void SecondBay_IsUsed_WhenTheFirstCannotFitTheBox()
    {
        var ship = Ship.Create("Testclad", 2160);
        ship.AddBay("Tiny", 1, 1, 1, 0, 0);
        ship.AddBay("Big", 8, 4, 2, 3, 0);
        var line = PickedUpLine("Baijini Point", ContainerSize.Two);

        var plan = StowagePlanner.Plan([.. ship.Bays], [.. line.Containers], line);

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.Values.Single().BayId, Is.EqualTo(ship.Bays.Last().Id));
    }

    [Test]
    public void ExistingPlacements_AreNeverMovedOrOverlapped()
    {
        var ship = OneBayShip();
        var first = PickedUpLine("Baijini Point", ContainerSize.Four);
        Apply(first, StowagePlanner.Plan([.. ship.Bays], [.. first.Containers], first)!);
        var frozen = first.Containers.Single().Placement;

        var second = PickedUpLine("Baijini Point", ContainerSize.Two);
        var plan = StowagePlanner.Plan([.. ship.Bays], [.. first.Containers], second);

        Assert.That(plan, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.Keys, Is.EquivalentTo(second.Containers.Select(c => c.Id)));
            Assert.That(first.Containers.Single().Placement, Is.EqualTo(frozen));
        }
    }

    [Test]
    public void OversizedBox_ReturnsNull()
    {
        var ship = OneBayShip(length: 1, width: 1, height: 1);
        var line = PickedUpLine("Baijini Point", ContainerSize.ThirtyTwo);

        Assert.That(StowagePlanner.Plan([.. ship.Bays], [.. line.Containers], line), Is.Null);
    }

    [Test]
    public void Plan_IsDeterministic()
    {
        var ship = OneBayShip();
        var line = PickedUpLine("Baijini Point", ContainerSize.Four, ContainerSize.Two, ContainerSize.One);

        var a = StowagePlanner.Plan([.. ship.Bays], [.. line.Containers], line);
        var b = StowagePlanner.Plan([.. ship.Bays], [.. line.Containers], line);

        Assert.That(a, Is.EqualTo(b).AsCollection);
    }

    [Test]
    public void FullTower_ContinuesInTheNextColumn()
    {
        var ship = OneBayShip(length: 6, width: 4, height: 2);
        var line = PickedUpLine("Baijini Point", ContainerSize.One, ContainerSize.One, ContainerSize.One);

        var plan = StowagePlanner.Plan([.. ship.Bays], [.. line.Containers], line);

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.Values.OrderBy(p => p.Y).ThenBy(p => p.Z).Select(p => (p.X, p.Y, p.Z)),
            Is.EqualTo([(0, 0, 0), (0, 0, 1), (0, 1, 0)]).AsCollection);
    }
}