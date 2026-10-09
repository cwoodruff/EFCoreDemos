using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using SpatialDemo.Api.Data;
using SpatialDemo.Api.Entities;
using ApiLocation = SpatialDemo.Api.Entities.Location;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// SpatialDemo.Api: the minimal-API endpoints run IsWithinDistance (/locations/near) and
    /// Polygon.Contains (/cities/{name}/locations) against geography columns in the spatialdemo database.
    /// These tests run the same LINQ the endpoints run, directly against AppDbContext.
    /// Writes happen inside a rolled-back transaction.
    /// </summary>
    public class SpatialDemoApiTests
    {
        private static readonly GeometryFactory Factory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        private static readonly Point TimesSquare = Factory.CreatePoint(new Coordinate(-73.9851, 40.7580));

        private static AppDbContext CreateContext() => new(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlServer(SqlServerTestServer.ConnectionString + "Database=spatialdemo;", x => x.UseNetTopologySuite())
                .Options);

        // Same query as GET /locations/near.
        private static Task<List<string>> NearAsync(AppDbContext db, Point origin, double radiusMeters) =>
            db.Locations
                .Where(l => l.Coordinates.IsWithinDistance(origin, radiusMeters))
                .Select(l => l.Name)
                .ToListAsync();

        // Same query as GET /cities/{cityName}/locations.
        private static async Task<List<string>?> InsideCityAsync(AppDbContext db, string cityName)
        {
            var city = await db.CityBoundaries.FirstOrDefaultAsync(c => c.CityName == cityName);
            if (city is null) return null;

            return await db.Locations
                .Where(l => city.Area.Contains(l.Coordinates))
                .Select(l => l.Name)
                .ToListAsync();
        }

        [Fact]
        public void Model_MapsPointAndPolygonToGeography()
        {
            using var db = CreateContext();

            var coordinates = db.Model.FindEntityType(typeof(ApiLocation))!.FindProperty(nameof(ApiLocation.Coordinates))!;
            var area = db.Model.FindEntityType(typeof(CityBoundary))!.FindProperty(nameof(CityBoundary.Area))!;

            Assert.Equal("geography", coordinates.GetColumnType());
            Assert.Equal("geography", area.GetColumnType());
        }

        [Fact]
        public void NearQuery_TranslatesToSTDistance()
        {
            using var db = CreateContext();

            var sql = db.Locations.Where(l => l.Coordinates.IsWithinDistance(TimesSquare, 1_000)).ToQueryString();

            Assert.Contains("STDistance", sql);
        }

        [SqlServerFact]
        public async Task Near_FiltersSeededLocationsByRadiusInMeters()
        {
            await using var db = CreateContext();

            // Seeded distances from Times Square: Empire State ~1.07 km, Central Park ~3.2 km.
            var within500 = await NearAsync(db, TimesSquare, 500);
            Assert.Contains("Times Square", within500);
            Assert.DoesNotContain("Empire State Building", within500);
            Assert.DoesNotContain("Central Park", within500);

            var within1500 = await NearAsync(db, TimesSquare, 1_500);
            Assert.Contains("Times Square", within1500);
            Assert.Contains("Empire State Building", within1500);
            Assert.DoesNotContain("Central Park", within1500);

            var within5000 = await NearAsync(db, TimesSquare, 5_000);
            Assert.Contains("Times Square", within5000);
            Assert.Contains("Empire State Building", within5000);
            Assert.Contains("Central Park", within5000);
        }

        [SqlServerFact]
        public async Task GetLocations_ProjectsLongitudeAndLatitudeFromPoint()
        {
            await using var db = CreateContext();

            // Same projection as GET /locations (X = longitude, Y = latitude).
            var timesSquare = await db.Locations
                .Where(l => l.Name == "Times Square")
                .Select(l => new { l.Id, l.Name, Longitude = l.Coordinates.X, Latitude = l.Coordinates.Y })
                .FirstAsync();

            Assert.Equal(-73.9851, timesSquare.Longitude, 6);
            Assert.Equal(40.7580, timesSquare.Latitude, 6);
        }

        [SqlServerFact]
        public async Task CityLocations_ReturnsSeededLocationsInsideNewYorkPolygon()
        {
            await using var db = CreateContext();

            var inside = await InsideCityAsync(db, "New York");

            Assert.NotNull(inside);
            Assert.Contains("Times Square", inside);
            Assert.Contains("Central Park", inside);
            Assert.Contains("Empire State Building", inside);

            // Unknown city -> the endpoint's NotFound path.
            Assert.Null(await InsideCityAsync(db, $"No Such City {Guid.NewGuid():N}"));
        }

        [SqlServerFact]
        public async Task PostLocation_ThenContains_SeparatesInsideFromOutside()
        {
            await using var db = CreateContext();
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                var suffix = Guid.NewGuid().ToString("N")[..8];
                var wallStreet = new ApiLocation { Name = $"Wall Street {suffix}", Coordinates = Factory.CreatePoint(new Coordinate(-74.0090, 40.7060)) };
                var brooklyn = new ApiLocation { Name = $"Brooklyn {suffix}", Coordinates = Factory.CreatePoint(new Coordinate(-73.9500, 40.6500)) };
                db.Locations.AddRange(wallStreet, brooklyn);
                await db.SaveChangesAsync();

                Assert.True(wallStreet.Id > 0);
                Assert.True(brooklyn.Id > 0);

                var inside = await InsideCityAsync(db, "New York");
                Assert.NotNull(inside);
                Assert.Contains(wallStreet.Name, inside);
                Assert.DoesNotContain(brooklyn.Name, inside);
            }
            finally
            {
                await tx.RollbackAsync();
            }
        }
    }
}
