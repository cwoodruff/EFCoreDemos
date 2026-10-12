using Microsoft.Data.SqlTypes;
using Microsoft.EntityFrameworkCore;

namespace vector_search.Data;

/// <summary>
/// One row per Chinook track: the text that was embedded and its 384-dimension vector.
/// Lives in the demo-owned VectorDemo database so the shared Chinook schema is unchanged.
/// </summary>
public class TrackEmbedding
{
    public int TrackId { get; set; }
    public string Track { get; set; } = null!;
    public string Artist { get; set; } = null!;
    public string Album { get; set; } = null!;
    public string Genre { get; set; } = null!;

    /// <summary>"{Track} by {Artist} from the album {Album}, genre {Genre}" - what the model saw.</summary>
    public string Text { get; set; } = null!;

    /// <summary>SQL Server 2025 vector(384), mapped by EF Core 10 as SqlVector&lt;float&gt;.</summary>
    public SqlVector<float> Embedding { get; set; }
}

public class VectorDemoContext(DbContextOptions<VectorDemoContext> options) : DbContext(options)
{
    public DbSet<TrackEmbedding> TrackEmbeddings => Set<TrackEmbedding>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrackEmbedding>(entity =>
        {
            entity.ToTable("TrackEmbedding");
            entity.HasKey(e => e.TrackId);
            entity.Property(e => e.TrackId).ValueGeneratedNever();
            entity.Property(e => e.Track).HasMaxLength(200);
            entity.Property(e => e.Artist).HasMaxLength(120);
            entity.Property(e => e.Album).HasMaxLength(160);
            entity.Property(e => e.Genre).HasMaxLength(120);
            entity.Property(e => e.Text).HasMaxLength(800);
            entity.Property(e => e.Embedding).HasColumnType("vector(384)");
        });
    }
}
