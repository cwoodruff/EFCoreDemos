using Microsoft.EntityFrameworkCore;

namespace linq_query_vs_fromsql_perf.Chinook;

/// <summary>
/// Minimal Chinook model (SQL Server) covering just the tables the sales report touches:
/// Invoice -> InvoiceLine -> Track -> Album.
/// </summary>
public class ChinookContext(DbContextOptions<ChinookContext> options) : DbContext(options)
{
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Track> Tracks => Set<Track>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();

    // Keyless, unmapped to any table/view: only ever populated by queries.
    public DbSet<AlbumSalesSummary> AlbumSalesSummaries => Set<AlbumSalesSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Album>(entity =>
        {
            entity.ToTable("Album");
            entity.Property(e => e.Title).HasMaxLength(160);
        });

        modelBuilder.Entity<Track>(entity =>
        {
            entity.ToTable("Track");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Composer).HasMaxLength(220);
            entity.Property(e => e.UnitPrice).HasColumnType("numeric(10, 2)");

            entity.HasOne(d => d.Album)
                .WithMany(p => p.Tracks)
                .HasForeignKey(d => d.AlbumId);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoice");
            entity.Property(e => e.InvoiceDate).HasColumnType("datetime");
            entity.Property(e => e.Total).HasColumnType("numeric(10, 2)");
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.ToTable("InvoiceLine");
            entity.Property(e => e.UnitPrice).HasColumnType("numeric(10, 2)");

            entity.HasOne(d => d.Invoice)
                .WithMany(p => p.InvoiceLines)
                .HasForeignKey(d => d.InvoiceId);

            entity.HasOne(d => d.Track)
                .WithMany(p => p.InvoiceLines)
                .HasForeignKey(d => d.TrackId);
        });

        modelBuilder.Entity<AlbumSalesSummary>(entity =>
        {
            entity.HasNoKey();
            entity.ToView(null);
            entity.Property(e => e.Revenue).HasColumnType("numeric(38, 2)");
        });
    }
}
