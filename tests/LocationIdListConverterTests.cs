using ScHauler.Data.Converters;
using ScHauler.Models;

namespace ScHauler.Tests;

[TestFixture]
public class LocationIdListConverterTests
{
    [Test]
    public void RoundTrips_EmptyAndThreeIds()
    {
        var converter = new LocationIdListConverter();
        var ids = new List<LocationId> { LocationId.New(), LocationId.New(), LocationId.New() };

        var stored = (string)converter.ConvertToProvider(ids)!;
        var back = (List<LocationId>)converter.ConvertFromProvider(stored)!;
        var emptyBack = (List<LocationId>)converter.ConvertFromProvider("")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(back, Is.EqualTo(ids).AsCollection);
            Assert.That(emptyBack, Is.Empty);
        }
    }
}
