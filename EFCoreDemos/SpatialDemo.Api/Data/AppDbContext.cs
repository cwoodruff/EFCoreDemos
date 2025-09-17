using Microsoft.EntityFrameworkCore;
using SpatialDemo.Api.Entities;

namespace SpatialDemo.Api.Data;

/// <summary>
/// Application DbContext configured for SQL Server geography + NetTopologySuite (SRID 4326).
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<CityBoundary> CityBoundaries => Set<CityBoundary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Location -> geography(Point)
        modelBuilder.Entity<Location>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);

            // Use SQL Server geography column for the Point
            e.Property(x => x.Coordinates)
                .HasColumnType("geography");
        });

        // CityBoundary -> geography(Polygon)
        modelBuilder.Entity<CityBoundary>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.CityName).IsRequired().HasMaxLength(200);

            // Use SQL Server geography column for the Polygon
            e.Property(x => x.Area)
                .HasColumnType("geography");
        });
    }
}