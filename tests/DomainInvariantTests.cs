using ScHauler.Models;

namespace ScHauler.Tests;

[TestFixture]
public class DomainInvariantTests
{
    private static CargoLine PendingLine(int scu = 10)
    {
        var contract = Contract.Create("Contract A", 0, DateTimeOffset.UnixEpoch);
        return contract.AddLine("Waste", new Scu(scu), TestLocations.Site("Everus Harbor"), TestLocations.Site("Baijini Point"));
    }

    private static Placement SomePlacement() => new(ShipBayId.New(), X: 0, Y: 0, Z: 0, Rotated: false);

    [TestCase(0)]
    [TestCase(-1)]
    public void Scu_RejectsNonPositiveValues(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = new Scu(value));
    }

    [Test]
    public void Contract_Create_RejectsBlankName()
    {
        Assert.Throws<ArgumentException>(() => Contract.Create("  ", 0, DateTimeOffset.UnixEpoch));
    }

    [Test]
    public void AddLine_RejectsSamePickupAndDropOff()
    {
        var contract = Contract.Create("Contract A", 0, DateTimeOffset.UnixEpoch);
        var location = TestLocations.Site("Everus Harbor");

        Assert.Throws<ArgumentException>(() => contract.AddLine("Waste", new Scu(1), location, location));
    }

    [Test]
    public void AddLine_StoresContractedScu_WithNoContainersYet()
    {
        var line = PendingLine(38);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(line.Scu, Is.EqualTo(new Scu(38)));
            Assert.That(line.Containers, Is.Empty);
        }
    }

    [Test]
    public void ContainerSize_FromScu_ReturnsTheSingleton()
    {
        Assert.That(ContainerSize.FromScu(8), Is.SameAs(ContainerSize.Eight));
    }

    [TestCase(0)]
    [TestCase(3)]
    [TestCase(64)]
    public void ContainerSize_FromScu_RejectsTheInvalidSizes(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ContainerSize.FromScu(value));
    }

    [Test]
    public void PickUp_CreatesContainers_AndSetsPickedUp()
    {
        var line = PendingLine(10);
        line.PickUp([ContainerSize.Eight, ContainerSize.Two]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(line.Status, Is.EqualTo(CargoLineStatus.PickedUp));
            Assert.That(line.Containers, Has.Count.EqualTo(2));
        }
    }

    [Test]
    public void PickUp_RejectsSumMismatch_AndChangesNothing()
    {
        var line = PendingLine(10);
        Assert.Throws<ArgumentException>(() => line.PickUp([ContainerSize.Eight]));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(line.Status, Is.EqualTo(CargoLineStatus.Pending));
            Assert.That(line.Containers, Is.Empty);
        }
    }

    [Test]
    public void PickUp_RejectsEmptySizes()
    {
        Assert.Throws<ArgumentException>(() => PendingLine().PickUp([]));
    }

    [Test]
    public void PickUp_WhenAlreadyPickedUp_Throws()
    {
        var line = PendingLine(10);
        line.PickUp([ContainerSize.Eight, ContainerSize.Two]);

        Assert.Throws<InvalidOperationException>(() => line.PickUp([ContainerSize.Eight, ContainerSize.Two]));
    }

    [Test]
    public void Correct_BackToPending_KeepsContainers()
    {
        var line = PendingLine(10);
        line.PickUp([ContainerSize.Eight, ContainerSize.Two]);

        line.Correct(CargoLineStatus.Pending);

        Assert.That(line.Containers, Has.Count.EqualTo(2));
    }

    [Test]
    public void PickUp_AfterCorrectBackToPending_ReplacesContainers()
    {
        var line = PendingLine(10);
        line.PickUp([ContainerSize.Eight, ContainerSize.Two]);
        line.Correct(CargoLineStatus.Pending);

        line.PickUp([ContainerSize.Four, ContainerSize.Four, ContainerSize.Two]);

        Assert.That(line.Containers, Has.Count.EqualTo(3));
    }

    [Test]
    public void Advance_FromPending_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PendingLine().Advance());
    }

    [Test]
    public void Advance_FromPickedUp_Delivers()
    {
        var line = PendingLine(10);
        line.PickUp([ContainerSize.Eight, ContainerSize.Two]);

        line.Advance();

        Assert.That(line.Status, Is.EqualTo(CargoLineStatus.Delivered));
    }

    [Test]
    public void Advance_FromDelivered_Throws()
    {
        var line = PendingLine(10);
        line.PickUp([ContainerSize.Eight, ContainerSize.Two]);
        line.Advance();

        Assert.Throws<InvalidOperationException>(() => line.Advance());
    }

    [Test]
    public void Contract_WithNoLines_IsNotCompleted()
    {
        Assert.That(Contract.Create("Contract A", 0, DateTimeOffset.UnixEpoch).IsCompleted, Is.False);
    }

    [TestCase(1, 1, 1, 1)]
    [TestCase(2, 2, 1, 1)]
    [TestCase(4, 2, 2, 1)]
    [TestCase(8, 2, 2, 2)]
    [TestCase(16, 4, 2, 2)]
    [TestCase(24, 6, 2, 2)]
    [TestCase(32, 8, 2, 2)]
    public void ContainerSize_HasExpectedFootprint(int scu, int length, int width, int height)
    {
        var size = ContainerSize.FromScu(scu);

        Assert.That((size.Length, size.Width, size.Height), Is.EqualTo((length, width, height)));
    }

    [Test]
    public void ContainerSize_FootprintVolume_EqualsScuValue()
    {
        Assert.That(ContainerSize.All.Select(s => s.Length * s.Width * s.Height), Is.EqualTo(ContainerSize.All.Select(s => s.Value)).AsCollection);
    }

    [Test]
    public void Place_WhileAboard_SetsPlacement()
    {
        var line = PendingLine(10);
        line.PickUp([ContainerSize.Eight, ContainerSize.Two]);
        var placement = SomePlacement();

        line.Containers.First().Place(placement);

        Assert.That(line.Containers.First().Placement, Is.EqualTo(placement));
    }

    [Test]
    public void Place_WhilePending_throws()
    {
        var line = PendingLine(10);
        line.PickUp([ContainerSize.Eight, ContainerSize.Two]);
        line.Correct(CargoLineStatus.Pending);

        Assert.Throws<InvalidOperationException>(() => line.Containers.First().Place(SomePlacement()));
    }

    [Test]
    public void Correct_BackToPending_ClearsPlacements()
    {
        var line = PendingLine(10);
        line.PickUp([ContainerSize.Eight, ContainerSize.Two]);
        line.Containers.First().Place(SomePlacement());

        line.Correct(CargoLineStatus.Pending);

        Assert.That(line.Containers.Select(c => c.Placement), Is.All.Null);
    }

    [Test]
    public void Advance_ToDelivered_KeepsPlacement()
    {
        var line = PendingLine(10);
        line.PickUp([ContainerSize.Eight, ContainerSize.Two]);
        line.Containers.First().Place(SomePlacement());

        line.Advance();

        Assert.That(line.Containers.First().Placement, Is.Not.Null);
    }

    [Test]
    public void Location_System_RejectsParent()
    {
        var stanton = Location.Create("Stanton", LocationKind.System, parent: null);

        Assert.Throws<ArgumentException>(() => Location.Create("Nope", LocationKind.System, stanton));
    }

    [Test]
    public void Location_Body_RequiresSystemParent()
    {
        var stanton = Location.Create("Stanton", LocationKind.System, parent: null);
        var hurston = Location.Create("Hurston", LocationKind.Body, stanton);

        Assert.Throws<ArgumentException>(() => Location.Create("Moonlet", LocationKind.Body, hurston));
    }

    [Test]
    public void Location_Site_RequiresBodyOrSystemParent()
    {
        Assert.Throws<ArgumentException>(() => Location.Create("Everus Harbor", LocationKind.Site, parent: null));
    }
}