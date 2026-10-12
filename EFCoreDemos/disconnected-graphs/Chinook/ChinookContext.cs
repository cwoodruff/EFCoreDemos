using Microsoft.EntityFrameworkCore;

namespace disconnected_graphs.Chinook;

/// <summary>
/// Minimal Chinook model (SQLite): just Album and its Tracks, the graph an "edit album" API sends back.
/// Track has no back-reference to Album so the graph serializes to JSON without cycles.
/// </summary>
public class ChinookContext(DbContextOptions<ChinookContext> options) : DbContext(options)
{
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Track> Tracks => Set<Track>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Album>(entity =>
        {
            entity.ToTable("Album");
            entity.Property(e => e.Title).HasMaxLength(160);

            // Track.AlbumId is nullable in Chinook, so this is an OPTIONAL relationship.
            entity.HasMany(e => e.Tracks)
                .WithOne()
                .HasForeignKey(t => t.AlbumId);
        });

        modelBuilder.Entity<Track>(entity =>
        {
            entity.ToTable("Track");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Composer).HasMaxLength(220);
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(10, 2)");
        });
    }
}

public class Album
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public int? ArtistId { get; set; }
    public List<Track> Tracks { get; set; } = [];
}

public class Track
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? AlbumId { get; set; }
    public int? MediaTypeId { get; set; }
    public int? GenreId { get; set; }
    public string? Composer { get; set; }
    public int? Milliseconds { get; set; }
    public int? Bytes { get; set; }
    public decimal? UnitPrice { get; set; }
}
