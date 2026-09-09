using ScHauler.Models;
using ScHauler.Services;

namespace ScHauler.Tests;

[TestFixture]
public class RouteNetTests
{
    private static readonly Location Stanton = Location.Create("Stanton", LocationKind.System, parent: null);
    private static readonly Location Pyro = Location.Create("Pyro", LocationKind.System, parent: null);
    private static readonly Location Hurston = Location.Create("Hurston", LocationKind.Body, parent: Stanton);
    private static readonly Location ArcCorp = Location.Create("ArcCorp", LocationKind.Body, parent: Stanton);
    private static readonly Location Everus = Location.Create("Everus Harbor", LocationKind.Site, parent: Hurston);
    private static readonly Location Lorville = Location.Create("Lorville", LocationKind.Site, parent: Hurston);
    private static readonly Location Baijini = Location.Create("Baijini Point", LocationKind.Site, parent: ArcCorp);
    private static readonly Location ArcL1 = Location.Create("ARC-L1", LocationKind.Site, parent: Stanton);
    private static readonly Location Ruin = Location.Create("Ruin Station", LocationKind.Site, parent: Pyro);
    private static RouteNet Net() => RouteNet.From([Stanton, Pyro, Hurston, ArcCorp, Everus, Lorville, Baijini, ArcL1, Ruin]);

    [Test]
    public void SameLocation_IsZeroHops()
    {
        Assert.That(Net().Hops(Everus.Id, Everus.Id), Is.Zero);
    }

    [Test]
    public void SameBody_IsOneHop()
    {
        Assert.That(Net().Hops(Everus.Id, Lorville.Id), Is.EqualTo(1));
    }

    [Test]
    public void SameSystem_IsTwoHops()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Net().Hops(Everus.Id, Baijini.Id), Is.EqualTo(2));
            Assert.That(Net().Hops(Everus.Id, ArcL1.Id), Is.EqualTo(2), "Lagrange station shares only the system");
        }
    }

    [Test]
    public void CrossSystem_IsThreeHops()
    {
        Assert.That(Net().Hops(Everus.Id, Ruin.Id), Is.EqualTo(3));
    }

    [Test]
    public void Hops_IsSymmetric()
    {
        Assert.That(Net().Hops(Baijini.Id, Everus.Id), Is.EqualTo(Net().Hops(Everus.Id, Baijini.Id)));
    }
}