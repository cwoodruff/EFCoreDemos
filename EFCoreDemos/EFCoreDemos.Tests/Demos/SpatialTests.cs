extern alias spatial;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using spatial::spatial.Models;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// spatial: NetTopologySuite types map to SQL Server geography, and Distance/IsWithinDistance in LINQ
    /// translate to STDistance against WideWorldImporters Application.Cities. Read-only.
    /// </summary>
    public class SpatialTests
    {
        private static readonly GeometryFactory Factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        // Same point the demo uses (Hudsonville, Michigan area). X = longitude, Y = latitude.
        private static Point DemoLocation() => Factory.CreatePoint(new Coordinate(-85.7693607, 42.8613121));

        [Fact]
        public void Model_MapsCityLocationToGeography()
        {
            using var db = new WideWorldImportersContext();
            var location = db.Model.FindEntityType(typeof(Cities))!.FindProperty(nameof(Cities.Location))!;

            Assert.Equal(typeof(Point), location.ClrType);
            Assert.Equal("geography", location.GetColumnType());
        }

        [Fact]
        public void OrderByDistance_TranslatesToSTDistance()
        {
            using var db = new WideWorldImportersContext();
            var here = DemoLocation();

            var sql = db.Cities.OrderBy(c => c.Location.Distance(here)).Select(c => c.CityName).Take(1).ToQueryString();

            Assert.Contains("STDistance", sql);
            Assert.Contains("ORDER BY", sql);
        }

        [SqlServerFact]
        public async Task NearestCity_ToDemoLocation_IsGrandville()
        {
            await using var db = new WideWorldImportersContext();
            var here = DemoLocation();

            var nearest = await db.Cities
                .OrderBy(c => c.Location.Distance(here))
                .Select(c => new { c.CityName, Meters = c.Location.Distance(here) })
                .FirstAsync();

            Assert.Equal("Grandville", nearest.CityName);
            Assert.InRange(nearest.Meters, 4_000, 7_000);
        }

        [SqlServerFact]
        public async Task IsWithinDistance_ReturnsOnlyCitiesInsideRadius_SortedByDistance()
        {
            await using var db = new WideWorldImportersContext();
            var here = DemoLocation();
            const double radiusMeters = 10_000;

            var nearby = await db.Cities
                .Where(c => c.Location.IsWithinDistance(here, radiusMeters))
                .OrderBy(c => c.Location.Distance(here))
                .Select(c => new { c.CityName, Meters = c.Location.Distance(here) })
                .ToListAsync();

            Assert.NotEmpty(nearby);
            Assert.Contains(nearby, c => c.CityName == "Grandville");
            Assert.Contains(nearby, c => c.CityName == "Jenison");
            Assert.All(nearby, c => Assert.True(c.Meters <= radiusMeters, $"{c.CityName} is {c.Meters} m away"));
            Assert.Equal(nearby.Select(c => c.Meters).Order(), nearby.Select(c => c.Meters));

            // Shrinking the radius only removes cities.
            var closer = await db.Cities.CountAsync(c => c.Location.IsWithinDistance(here, 6_000));
            Assert.InRange(closer, 1, nearby.Count);
        }

        [SqlServerFact]
        public async Task Distance_IsComputedOnServerInMeters()
        {
            await using var db = new WideWorldImportersContext();
            var here = DemoLocation();

            // Geography distances come back in meters: Grand Rapids, MI is ~14 km away (a planar distance in degrees would be ~0.1).
            // WideWorldImporters has several Grand Rapids; take the nearest one.
            var meters = await db.Cities
                .Where(c => c.CityName == "Grand Rapids")
                .Select(c => c.Location.Distance(here))
                .OrderBy(d => d)
                .FirstAsync();

            Assert.InRange(meters, 10_000, 20_000);
        }
    }
}
