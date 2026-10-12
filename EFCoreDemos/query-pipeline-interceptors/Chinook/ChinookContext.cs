using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace query_pipeline_interceptors.Chinook;

/// <summary>
/// Minimal Chinook model (SQLite): Album -> Track. Interceptors are supplied through the options
/// (see <see cref="Demo.Options"/>), so the context itself knows nothing about them.
/// </summary>
public class ChinookContext(DbContextOptions<ChinookContext> options) : DbContext(options)
{
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Track> Tracks => Set<Track>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Album>().ToTable("Album");

        modelBuilder.Entity<Track>(entity =>
        {
            entity.ToTable("Track");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(10, 2)");
            entity.HasOne(t => t.Album).WithMany(a => a.Tracks).HasForeignKey(t => t.AlbumId);
        });
    }
}

/// <summary>Entities that record when they were read from the database.</summary>
public interface IHasRetrievedAt
{
    DateTimeOffset? RetrievedAt { get; set; }
}

public class Album : IHasRetrievedAt
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public int? ArtistId { get; set; }
    public List<Track> Tracks { get; } = [];

    [NotMapped]
    public DateTimeOffset? RetrievedAt { get; set; }
}

public class Track : IHasRetrievedAt
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? AlbumId { get; set; }
    public Album? Album { get; set; }
    public string? Composer { get; set; }
    public int? Milliseconds { get; set; }
    public decimal? UnitPrice { get; set; }

    [NotMapped]
    public DateTimeOffset? RetrievedAt { get; set; }

    /// <summary>A service handed to the entity by the materialization interceptor - not a column.</summary>
    [NotMapped]
    public IDurationFormatter? Formatter { get; set; }

    /// <summary>"5:43" - computed by the injected service, so the entity needs no formatting code.</summary>
    public string Length => Formatter?.Format(Milliseconds) ?? "(no formatter)";
}

/// <summary>A tiny application service the materialization interceptor injects into every Track.</summary>
public interface IDurationFormatter
{
    string Format(int? milliseconds);
}

public sealed class MinutesSecondsFormatter : IDurationFormatter
{
    public string Format(int? milliseconds) =>
        milliseconds is { } ms ? TimeSpan.FromMilliseconds(ms).ToString(@"m\:ss") : "?";
}
