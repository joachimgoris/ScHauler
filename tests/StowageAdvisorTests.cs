using ScHauler.Models;
using ScHauler.Services;

namespace ScHauler.Tests;

[TestFixture]
public class StowageAdvisorTests
{
    private static Ship Ironclad()
    {
        var ship = Ship.Create("Ironclad", 2160);
        ship.AddBay("Fwd port", 20, 6, 6, 0, 0);
        ship.AddBay("Fwd starboard", 20, 6, 6, 0, 7);
        ship.AddBay("Aft port", 10, 6, 6, 21, 0);
        ship.AddBay("Aft starboard", 10, 6, 6, 21, 7);
        return ship;
    }

    private static CargoLine PickedUpLine(string dropOff, params ContainerSize[] sizes)
    {
        var contract = Contract.Create("C", 0, DateTimeOffset.UnixEpoch);
        var line = contract.AddLine("Waste", new Scu(sizes.Sum(s => s.Value)), TestLocations.Site("Everus Harbor"), TestLocations.Site(dropOff));
        line.PickUp(sizes);
        return line;
    }

    [TestCase(500, 32, "32,32,32,32,32,32,32,32,32,32,32,32,32,32,32,16,4")]
    [TestCase(20, 8, "8,8,4")]
    [TestCase(7, 32, "4,2,1")]
    public void Split_GreedyDescending_NoBoxLargerThanMax(int scu, int max, string expected)
    {
        var boxes = StowageAdvisor.Split(scu, ContainerSize.FromScu(max));

        Assert.That(string.Join(",", boxes.Select(b => b.Value)), Is.EqualTo(expected));
    }

    [Test]
    public void EmptyHold_FitsAsThirtyTwos_Planned()
    {
        var ship = Ironclad();
        var destination = TestLocations.Site("Lorville").Id;

        var advice = StowageAdvisor.LargestBoxThatFits([.. ship.Bays], [], destination, 500, [destination]);

        Assert.That(advice, Is.EqualTo(new FitAdvice(ContainerSize.ThirtyTwo, StowTier.Planned)));
    }

    [Test]
    public void FieldLoad_SevenHundredMore_DoesNotFit_AndNinetySixOnlyAsTwos()
    {
        // The 2026-10-21 field load (1,822 SCU aboard, six drops) leaves 338 scattered cells: 700 SCU is refused before any plan runs (cell count), 96 SCU fits only as 2 SCU boxes with piles touching, 128 not at all.
        var ship = Ironclad();
        var bays = ship.Bays.ToList();
        static ContainerSize[] Boxes(int count) => [.. Enumerable.Repeat(ContainerSize.ThirtyTwo, count)];
        var lines = new[]
        {
            PickedUpLine("Lorville", Boxes(15)),
            PickedUpLine("Area18", ContainerSize.TwentyFour),
            PickedUpLine("Baijini Point", ContainerSize.ThirtyTwo, ContainerSize.Eight, ContainerSize.Two),
            PickedUpLine("New Babbage", Boxes(10)),
            PickedUpLine("Orison", Boxes(8)),
            PickedUpLine("Seraphim Station", [.. Boxes(4), .. Enumerable.Repeat(ContainerSize.Sixteen, 30), .. Enumerable.Repeat(ContainerSize.Eight, 11), ContainerSize.Four]),
        };
        var dropOrder = lines.Select(l => l.DropOffLocationId).ToList();
        var repack = StowagePlanner.Repack(bays, lines, dropOrder, allowTouching: true)!;
        foreach (var container in lines.SelectMany(l => l.Containers))
        {
            container.Place(repack[container.Id]);
        }

        var everus = TestLocations.Site("Everus Harbor").Id;
        var order = dropOrder.Append(everus).ToList();
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var tooMuch = StowageAdvisor.LargestBoxThatFits(bays, lines, everus, 700, order);
        var some = StowageAdvisor.LargestBoxThatFits(bays, lines, everus, 96, order);
        var none = StowageAdvisor.LargestBoxThatFits(bays, lines, everus, 128, order);
        watch.Stop();

        TestContext.Out.WriteLine($"advice: 700 -> {tooMuch?.ToString() ?? "no fit"}, 96 -> {some?.ToString() ?? "no fit"}, 128 -> {none?.ToString() ?? "no fit"} in {watch.ElapsedMilliseconds} ms");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(tooMuch, Is.Null);
            Assert.That(some, Is.EqualTo(new FitAdvice(ContainerSize.Two, StowTier.Tight)));
            Assert.That(none, Is.Null);
            Assert.That(watch.ElapsedMilliseconds, Is.LessThan(5000), "three advice calls, worst case 7 sizes x 4 tiers each");
        }
    }
}
