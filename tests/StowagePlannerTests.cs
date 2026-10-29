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
    public void TightStow_LetsPilesTouch_ButNeverStackAcrossDestinations()
    {
        // 2x1x2 bay: A on the floor at x=0; B may sit beside it (x=1) when touching is allowed, never on top of it.
        var ship = OneBayShip(length: 2, width: 1, height: 2);
        var bay = ship.Bays.First();
        var first = PickedUpLine("Baijini Point", ContainerSize.One);
        first.Containers.Single().Place(new Placement(bay.Id, 0, 0, 0, Rotated: false));
        var second = PickedUpLine("Area18", ContainerSize.One);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(StowagePlanner.Plan([.. ship.Bays], [.. first.Containers], second), Is.Null);
            Assert.That(StowagePlanner.Plan([.. ship.Bays], [.. first.Containers], second, allowTouching: true)?.Values.Single(), Is.EqualTo(new Placement(bay.Id, 1, 0, 0, Rotated: false)));
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

    [Test]
    public void ReservedRegion_IsPreferred_OverTheOrigin()
    {
        var ship = OneBayShip();
        var bay = ship.Bays.First();
        var line = PickedUpLine("Baijini Point", ContainerSize.One);
        var reserved = new HashSet<HoldCell> { new(bay.Id, 5, 0, 0) };

        var plan = StowagePlanner.Plan([.. ship.Bays], [.. line.Containers], line, reserved);

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.Values.Single(), Is.EqualTo(new Placement(bay.Id, 5, 0, 0, Rotated: false)));
    }

    [Test]
    public void OversizedBox_IsPulledToTheReservedRegionsEdge()
    {
        // The region is one cell; a 2x1 box cannot be inside it, so the candidate with the fewest cells outside wins.
        var ship = OneBayShip();
        var bay = ship.Bays.First();
        var line = PickedUpLine("Baijini Point", ContainerSize.Two);
        var reserved = new HashSet<HoldCell> { new(bay.Id, 5, 0, 0) };

        var plan = StowagePlanner.Plan([.. ship.Bays], [.. line.Containers], line, reserved);

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.Values.Single(), Is.EqualTo(new Placement(bay.Id, 4, 0, 0, Rotated: false)));
    }

    private static Ship Ironclad()
    {
        var ship = Ship.Create("Ironclad", 2160);
        ship.AddBay("Fwd port", 20, 6, 6, 0, 0);
        ship.AddBay("Fwd starboard", 20, 6, 6, 0, 7);
        ship.AddBay("Aft port", 10, 6, 6, 21, 0);
        ship.AddBay("Aft starboard", 10, 6, 6, 21, 7);
        return ship;
    }

    [Test]
    public void FifteenLargeBoxes_PlanWithinATabletTap()
    {
        // 480 SCU as 15 x 32 SCU boxes into an empty Ironclad with a seven-slice reserved slab: the field case that took 6.8 s before pruning.
        var ship = Ironclad();
        var bays = ship.Bays.ToList();
        var line = PickedUpLine("Lorville", [.. Enumerable.Repeat(ContainerSize.ThirtyTwo, 15)]);
        var reserved = bays.Take(2)
            .SelectMany(b => from x in Enumerable.Range(10, 7) from y in Enumerable.Range(0, 6) from z in Enumerable.Range(0, 6) select new HoldCell(b.Id, x, y, z))
            .ToHashSet();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var plan = StowagePlanner.Plan(bays, [], line, reserved);
        watch.Stop();

        TestContext.Out.WriteLine($"15 x 32 SCU planned in {watch.ElapsedMilliseconds} ms");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan, Is.Not.Null);
            Assert.That(plan, Has.Count.EqualTo(15));
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(500));
        }
    }

    [Test]
    public void Box_AvoidsSittingInFrontOfAnEarlierDropsCargo()
    {
        // Lorville (drop 1) is aboard at x=5 in lanes 0-1; the drop-2 box prefers the free lane over the origin in front of it.
        var ship = OneBayShip(length: 10, width: 4, height: 2);
        var bay = ship.Bays.First();
        var earlier = PickedUpLine("Lorville", ContainerSize.Four);
        earlier.Containers.Single().Place(new Placement(bay.Id, 5, 0, 0, Rotated: false));
        var line = PickedUpLine("Area18", ContainerSize.One);

        var plan = StowagePlanner.Plan([.. ship.Bays], [.. earlier.Containers], line, reserved: null, dropOrder: [earlier.DropOffLocationId, line.DropOffLocationId]);

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.Values.Single(), Is.EqualTo(new Placement(bay.Id, 0, 2, 0, Rotated: false)));
    }

    [Test]
    public void Box_MayUseAForwardBay_InFrontOfAnAftBaysEarlierCargo()
    {
        // Lorville (drop 1) is aboard in the aft bay; the forward bay's origin is not "in front of" it, so the drop-2 box takes it.
        var ship = Ship.Create("Testclad", 2160);
        var fwd = ship.AddBay("Fwd", 10, 2, 1, 0, 0);
        var aft = ship.AddBay("Aft", 10, 2, 1, 11, 0);
        var earlier = PickedUpLine("Lorville", ContainerSize.Four);
        earlier.Containers.Single().Place(new Placement(aft.Id, 0, 0, 0, Rotated: false));
        var line = PickedUpLine("Area18", ContainerSize.One);

        var plan = StowagePlanner.Plan([.. ship.Bays], [.. earlier.Containers], line, reserved: null, dropOrder: [earlier.DropOffLocationId, line.DropOffLocationId]);

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.Values.Single(), Is.EqualTo(new Placement(fwd.Id, 0, 0, 0, Rotated: false)));
    }

    [Test]
    public void Box_StillSitsInFrontOfAnEarlierDrop_WhenThereIsNoOtherSpot()
    {
        var ship = OneBayShip(length: 4, width: 2, height: 1);
        var bay = ship.Bays.First();
        var earlier = PickedUpLine("Lorville", ContainerSize.Four);
        earlier.Containers.Single().Place(new Placement(bay.Id, 2, 0, 0, Rotated: false));
        var line = PickedUpLine("Area18", ContainerSize.One);

        var plan = StowagePlanner.Plan([.. ship.Bays], [.. earlier.Containers], line, reserved: null, dropOrder: [earlier.DropOffLocationId, line.DropOffLocationId]);

        Assert.That(plan, Is.Not.Null);
        Assert.That(plan.Values.Single().X, Is.Zero);
    }

    [Test]
    public void Repack_PlacesEverything_WhenTheIncrementalPlanCannot()
    {
        // A lone box parked mid-lane fragments a 4x1x1 bay so a 2x1 box cannot be added; a repack lines both up from the ramp.
        var ship = OneBayShip(length: 4, width: 1, height: 1);
        var bay = ship.Bays.First();
        var first = PickedUpLine("Lorville", ContainerSize.One);
        first.Containers.Single().Place(new Placement(bay.Id, 2, 0, 0, Rotated: false));
        var second = PickedUpLine("Area18", ContainerSize.Two);
        var dropOrder = new[] { first.DropOffLocationId, second.DropOffLocationId };

        Assert.That(StowagePlanner.Plan([.. ship.Bays], [.. first.Containers], second, null, dropOrder), Is.Null);

        var repack = StowagePlanner.Repack([.. ship.Bays], [first, second], dropOrder);

        Assert.That(repack, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(repack[first.Containers.Single().Id], Is.EqualTo(new Placement(bay.Id, 0, 0, 0, Rotated: false)));
            Assert.That(repack[second.Containers.Single().Id].X, Is.EqualTo(2));
        }
    }

    [Test]
    public void Repack_KeepsEarlierDropsNearestTheRamp()
    {
        var ship = OneBayShip(length: 10, width: 2, height: 1);
        var first = PickedUpLine("Lorville", ContainerSize.Two);
        var second = PickedUpLine("Area18", ContainerSize.Two);
        var dropOrder = new[] { first.DropOffLocationId, second.DropOffLocationId };

        var repack = StowagePlanner.Repack([.. ship.Bays], [second, first], dropOrder);

        Assert.That(repack, Is.Not.Null);
        Assert.That(repack[first.Containers.Single().Id].X, Is.LessThan(repack[second.Containers.Single().Id].X));
    }

    [Test]
    public void Repack_ReturnsNull_WhenTheHoldCannotTakeItAll()
    {
        var ship = OneBayShip(length: 2, width: 1, height: 1);
        var first = PickedUpLine("Lorville", ContainerSize.One);
        var second = PickedUpLine("Area18", ContainerSize.One);

        Assert.That(StowagePlanner.Repack([.. ship.Bays], [first, second], [first.DropOffLocationId, second.DropOffLocationId]), Is.Null);
    }

    [Test]
    public void Repack_FillsTheHold_FieldScenario()
    {
        // 2026-10-21 field load: 1,122 SCU aboard across five drops, then a 700 SCU line (4 x 32 + 30 x 16 + 11 x 8 + 4). Must restow (tight) into the Ironclad; with the moat rule nothing over ~72 SCU fits, and an all-32 mix never can: 55 boxes, 54 slots.
        var ship = Ironclad();
        var bays = ship.Bays.ToList();
        static ContainerSize[] Boxes(int count) => [.. Enumerable.Repeat(ContainerSize.ThirtyTwo, count)];
        var baijini = PickedUpLine("Baijini Point", ContainerSize.ThirtyTwo, ContainerSize.Eight, ContainerSize.Two);
        var area18 = PickedUpLine("Area18", ContainerSize.TwentyFour);
        var lorville = PickedUpLine("Lorville", Boxes(15));
        var newBabbage = PickedUpLine("New Babbage", Boxes(10));
        var orison = PickedUpLine("Orison", Boxes(8));
        var seraphim = PickedUpLine("Seraphim Station", [.. Boxes(4), .. Enumerable.Repeat(ContainerSize.Sixteen, 30), .. Enumerable.Repeat(ContainerSize.Eight, 11), ContainerSize.Four]);
        var lines = new[] { lorville, area18, baijini, newBabbage, orison, seraphim };
        var dropOrder = lines.Select(l => l.DropOffLocationId).ToList();

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var repack = StowagePlanner.Repack(bays, lines, dropOrder, allowTouching: true);
        watch.Stop();

        TestContext.Out.WriteLine($"field repack: {(repack is null ? "no fit" : $"{repack.Count} boxes")} in {watch.ElapsedMilliseconds} ms");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(StowagePlanner.Repack(bays, lines, dropOrder), Is.Null, "with the moat rule this load does not fit");
            Assert.That(repack, Is.Not.Null);
            Assert.That(repack, Has.Count.EqualTo(lines.Sum(l => l.Containers.Count)));
        }
    }
}
