using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using SpatialDemo.Api.Data;
using SpatialDemo.Api.Entities;
using SpatialDemo.Api.Models;
using Location = SpatialDemo.Api.Entities.Location;


var builder = WebApplication.CreateBuilder(args);

// Add DbContext (SQL Server + NetTopologySuite)
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("DefaultConnection")
              ?? "Server=localhost,1433;Database=spatialdemo;User=sa;Password=8Riwudeg!!;Trusted_Connection=False;MultipleActiveResultSets=true;TrustServerCertificate=true;Application Name=SpatialDemo.Api";
    options.UseSqlServer(conn, x => x.UseNetTopologySuite());
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Apply migrations automatically in dev
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    
    // Seed if empty
    if (!db.Locations.Any())
    {
        var gf = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

        db.Locations.AddRange(
            new Location { Name = "Times Square", Coordinates = gf.CreatePoint(new Coordinate(-73.9851, 40.7580)) },
            new Location { Name = "Central Park", Coordinates = gf.CreatePoint(new Coordinate(-73.9654, 40.7829)) },
            new Location { Name = "Empire State Building", Coordinates = gf.CreatePoint(new Coordinate(-73.9857, 40.7484)) }
        );

        // Simple Manhattan-ish rectangle polygon (not accurate, for demo)
        var outer = gf.CreateLinearRing([
            new Coordinate(-74.02, 40.70),
            new Coordinate(-73.93, 40.70),
            new Coordinate(-73.93, 40.83),
            new Coordinate(-74.02, 40.83),
            new Coordinate(-74.02, 40.70)
        ]);
        var manhattan = gf.CreatePolygon(outer);
        db.CityBoundaries.Add(new CityBoundary { CityName = "New York", Area = manhattan });

        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Redirect("/swagger"));

// Create a location
app.MapPost("/locations", async (AppDbContext db, LocationDto dto) =>
{
    var gf = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
    var point = gf.CreatePoint(new Coordinate(dto.Longitude, dto.Latitude));
    var loc = new Location { Name = dto.Name, Coordinates = point };
    db.Locations.Add(loc);
    await db.SaveChangesAsync();
    return Results.Created($"/locations/{loc.Id}", new { loc.Id, loc.Name });
});

// Get all locations
app.MapGet("/locations", async (AppDbContext db) =>
{
    var list = await db.Locations
        .Select(l => new { l.Id, l.Name, Longitude = l.Coordinates.X, Latitude = l.Coordinates.Y })
        .ToListAsync();
    return Results.Ok(list);
});

// Find nearby locations within a radius (meters)
app.MapGet("/locations/near", async (AppDbContext db, double longitude, double latitude, double radiusMeters) =>
{
    var gf = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
    var origin = gf.CreatePoint(new Coordinate(longitude, latitude));

    var results = await db.Locations
        .Where(l => l.Coordinates.IsWithinDistance(origin, radiusMeters))
        .Select(l => new { l.Id, l.Name, Longitude = l.Coordinates.X, Latitude = l.Coordinates.Y })
        .ToListAsync();

    return Results.Ok(results);
})
.WithOpenApi(op =>
{
    op.Summary = "Find nearby locations within a radius (meters)";
    op.Parameters[0].Description = "Longitude (X)";
    op.Parameters[1].Description = "Latitude (Y)";
    op.Parameters[2].Description = "Radius in meters";
    return op;
});

// Create a city boundary polygon from a list of coordinates (lon/lat)
app.MapPost("/cities", async (AppDbContext db, CityBoundaryDto dto) =>
{
    var gf = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    // Build LinearRing (must end where it starts)
    var coords = dto.Coordinates.Select(c => new Coordinate(c.Longitude, c.Latitude)).ToList();
    if (coords.Count == 0 || (coords[0].X != coords[^1].X || coords[0].Y != coords[^1].Y))
    {
        coords.Add(new Coordinate(coords[0].X, coords[0].Y)); // close ring
    }
    var ring = gf.CreateLinearRing(coords.ToArray());
    var poly = gf.CreatePolygon(ring);

    var city = new CityBoundary { CityName = dto.CityName, Area = poly };
    db.CityBoundaries.Add(city);
    await db.SaveChangesAsync();
    return Results.Created($"/cities/{city.Id}", new { city.Id, city.CityName });
});

// List cities
app.MapGet("/cities", async (AppDbContext db) =>
{
    var cities = await db.CityBoundaries
        .Select(c => new { c.Id, c.CityName })
        .ToListAsync();
    return Results.Ok(cities);
});

// Get locations inside a specific city by name
app.MapGet("/cities/{cityName}/locations", async (AppDbContext db, string cityName) =>
{
    var city = await db.CityBoundaries.FirstOrDefaultAsync(c => c.CityName == cityName);
    if (city is null) return Results.NotFound(new { message = "City not found" });

    var inside = await db.Locations
        .Where(l => city.Area.Contains(l.Coordinates))
        .Select(l => new { l.Id, l.Name, Longitude = l.Coordinates.X, Latitude = l.Coordinates.Y })
        .ToListAsync();

    return Results.Ok(inside);
});

app.Run();

namespace SpatialDemo.Api.Models
{
    public record LocationDto(string Name, double Longitude, double Latitude);
    public record CityBoundaryDto(string CityName, List<LonLat> Coordinates);
    public record LonLat(double Longitude, double Latitude);
}
