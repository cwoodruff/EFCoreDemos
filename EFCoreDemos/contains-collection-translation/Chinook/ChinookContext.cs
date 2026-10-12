using Microsoft.EntityFrameworkCore;

namespace contains_collection_translation.Chinook;

/// <summary>
/// Minimal Chinook model (SQL Server): just the Track table, which is all the Contains queries touch.
/// </summary>
public class ChinookContext(DbContextOptions<ChinookContext> options) : DbContext(options)
{
    public DbSet<Track> Tracks => Set<Track>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Track>(entity =>
        {
            entity.ToTable("Track");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Composer).HasMaxLength(220);
            entity.Property(e => e.UnitPrice).HasColumnType("numeric(10, 2)");
        });
    }
}
