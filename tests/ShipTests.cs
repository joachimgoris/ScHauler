using ScHauler.Models;

namespace ScHauler.Tests;

[TestFixture]
public class ShipTests
{
    private static Ship Ironclad() => Ship.Create("Ironclad", 2204);

    [Test]
    public void Create_RejectsBlankName()
    {
        Assert.Throws<ArgumentException>(() => Ship.Create("", 0));
    }

    [Test]
    public void Create_RejectsNegativeCapacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Ship.Create("Ironclad", -1));
    }

    [Test]
    public void SetCapacity_RejectsNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Ironclad().SetCapacity(-1));
    }

    [Test]
    public void AddBay_StoresDimsAndOffsets()
    {
        var ship = Ironclad();

        var bay = ship.AddBay("Fwd port", length: 20, width: 6, height: 6, drawOffsetX: 0, drawOffsetY: 0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That((bay.Length, bay.Width, bay.Height), Is.EqualTo((20, 6, 6)));
            Assert.That(ship.Bays, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public void AddBay_RejectsDuplicateName_CaseInsensitive()
    {
        var ship = Ironclad();
        ship.AddBay("Fwd port", 20, 6, 6, 0, 0);

        Assert.Throws<ArgumentException>(() => ship.AddBay("FWD PORT", 10, 6, 6, 21, 0));
    }

    [TestCase(0, 6, 6)]
    [TestCase(20, -1, 6)]
    [TestCase(20, 6, 0)]
    public void AddBay_RejectsNonPositiveDimensions(int length, int width, int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Ironclad().AddBay("Bad bay", length, width, height, 0, 0));
    }

    [Test]
    public void AddBay_RejectsNegativeOffsets()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Ironclad().AddBay("Bad bay", 20, 6, 6, -1, 0));
    }

    [Test]
    public void MoveTo_SetsCurrentLocation()
    {
        var ship = Ironclad();
        var location = TestLocations.Site("Everus Harbor");

        ship.MoveTo(location.Id);

        Assert.That(ship.CurrentLocationId, Is.EqualTo(location.Id));
    }

    [Test]
    public void PinDropOrder_RoundTrips_AndMoveKeepsIt()
    {
        var ship = Ironclad();
        var order = new[] { TestLocations.Site("Lorville").Id, TestLocations.Site("Area18").Id };

        ship.PinDropOrder(order);
        ship.MoveTo(TestLocations.Site("Everus Harbor").Id);

        Assert.That(ship.PinnedDropOrder, Is.EqualTo(order).AsCollection);
    }

    [Test]
    public void PinDropOrder_RejectsDuplicates()
    {
        var id = TestLocations.Site("Lorville").Id;

        Assert.Throws<ArgumentException>(() => Ironclad().PinDropOrder([id, id]));
    }

    [Test]
    public void ClearDropOrder_Empties()
    {
        var ship = Ironclad();
        ship.PinDropOrder([TestLocations.Site("Lorville").Id]);

        ship.ClearDropOrder();

        Assert.That(ship.PinnedDropOrder, Is.Empty);
    }
}
