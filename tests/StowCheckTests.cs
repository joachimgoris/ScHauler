using ScHauler.Models;
using ScHauler.Services;

namespace ScHauler.Tests;

[TestFixture]
public class StowCheckTests
{
    private static HoldBoxView Box(string dest, int x) => new(x, 0, 0, 1, 1, 1, 1, dest);

    private static HoldBayView Bay(int drawOffsetX, params HoldBoxView[] boxes) => new("Bay", 30, 6, 6, drawOffsetX, 0, boxes);

    [Test]
    public void StowCheck_Empty_WhenStowMatchesDropOrder()
    {
        var hold = new HoldView([Bay(0, Box("Alpha", 2), Box("Bravo", 5))], 0, []);

        Assert.That(ManifestPlanner.StowCheck(hold, ["Alpha", "Bravo"]), Is.Empty);
    }

    [Test]
    public void StowCheck_FlagsEarlierDropBuriedBehindLaterDrop()
    {
        var hold = new HoldView([Bay(0, Box("Alpha", 5), Box("Bravo", 2))], 0, []);

        Assert.That(ManifestPlanner.StowCheck(hold, ["Alpha", "Bravo"]), Is.EqualTo([new StowConflict("Alpha", "Bravo")]).AsCollection);
    }

    [Test]
    public void StowCheck_IgnoresCargoInOtherBays()
    {
        // The walkway gives access to the aft bay: forward-bay cargo never buries aft-bay cargo, whatever the lane.
        var hold = new HoldView([Bay(0, Box("Bravo", 5)), Bay(21, Box("Alpha", 0))], 0, []);

        Assert.That(ManifestPlanner.StowCheck(hold, ["Alpha", "Bravo"]), Is.Empty);
    }

    [Test]
    public void StowCheck_ComparesLanesWithinOneBay_NotAcrossTheCanvas()
    {
        // Two bays stacked in the canvas (DrawOffsetY 0 and 7) share no lanes: Alpha in lane 0 of the second bay is not behind Bravo in lane 0 of the first.
        var hold = new HoldView([new HoldBayView("Fwd", 20, 6, 6, 0, 0, [Box("Bravo", 2)]), new HoldBayView("Stbd", 20, 6, 6, 0, 7, [Box("Alpha", 9)])], 0, []);

        Assert.That(ManifestPlanner.StowCheck(hold, ["Alpha", "Bravo"]), Is.Empty);
    }

    [Test]
    public void StowCheck_IgnoresCargoInOtherLanes()
    {
        // Side by side is not buried: Alpha deeper but in lane 3, Bravo in lane 0.
        var hold = new HoldView([Bay(0, new HoldBoxView(5, 3, 0, 1, 1, 1, 1, "Alpha"), Box("Bravo", 2))], 0, []);

        Assert.That(ManifestPlanner.StowCheck(hold, ["Alpha", "Bravo"]), Is.Empty);
    }

    [Test]
    public void StowCheck_SeesAWideBoxAcrossItsLanes()
    {
        // Bravo is 2 wide at lanes 0-1; Alpha's box in lane 1 behind it is buried.
        var hold = new HoldView([Bay(0, new HoldBoxView(5, 1, 0, 1, 1, 1, 1, "Alpha"), new HoldBoxView(2, 0, 0, 1, 2, 2, 1, "Bravo"))], 0, []);

        Assert.That(ManifestPlanner.StowCheck(hold, ["Alpha", "Bravo"]), Has.Count.EqualTo(1));
    }

    [Test]
    public void StowCheck_IgnoresDestinationsWithNothingAboard()
    {
        var hold = new HoldView([Bay(0, Box("Bravo", 2))], 0, []);

        Assert.That(ManifestPlanner.StowCheck(hold, ["Alpha", "Bravo"]), Is.Empty);
    }
}

[TestFixture]
public class GhostProjectionTests
{
    [Test]
    public void Ghosts_ProjectRegionColumnsTopDown_InCanvasCells()
    {
        var ship = Ship.Create("Testclad", 2160);
        var aft = ship.AddBay("Aft", 10, 6, 6, 21, 7);
        var lorville = TestLocations.Site("Lorville");
        var regions = new Dictionary<LocationId, IReadOnlySet<HoldCell>>
        {
            [lorville.Id] = new HashSet<HoldCell> { new(aft.Id, 5, 0, 0), new(aft.Id, 5, 0, 1), new(aft.Id, 5, 1, 0) },
        };

        var ghosts = ManifestPlanner.BuildGhosts([.. ship.Bays], regions, [new Destination(lorville.Id, "Lorville", 0, 3)]);

        Assert.That(ghosts, Is.EqualTo([new HoldGhostView(26, 7, 2, "Lorville"), new HoldGhostView(26, 8, 1, "Lorville")]).AsCollection);
    }
}
