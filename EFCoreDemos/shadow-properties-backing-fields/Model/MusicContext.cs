using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace shadow_properties_backing_fields.Model;

/// <summary>
/// All the persistence detail lives here, not in Album/Track: shadow audit columns,
/// the backing field, and model-wide conventions.
/// </summary>
public class MusicContext(DbContextOptions<MusicContext> options, Func<DateTime>? clock = null) : DbContext(options)
{
    public const string CreatedAt = nameof(CreatedAt);
    public const string LastUpdated = nameof(LastUpdated);

    private readonly Func<DateTime> _clock = clock ?? (() => DateTime.UtcNow);

    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Track> Tracks => Set<Track>();

    public static DbContextOptions<MusicContext> CreateOptions(string connectionString, Action<string>? log = null)
    {
        var builder = new DbContextOptionsBuilder<MusicContext>().UseSqlite(connectionString);
        if (log != null)
        {
            builder.LogTo(log, [DbLoggerCategory.Database.Command.Name], LogLevel.Information);
        }

        return builder.Options;
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Model-wide defaults instead of HasMaxLength / HasPrecision on every property.
        configurationBuilder.Properties<string>().HaveMaxLength(200);
        configurationBuilder.Properties<decimal>().HavePrecision(10, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Album>(album =>
        {
            // Explicit configuration beats the convention: titles are shorter than the 200 default.
            album.Property(a => a.Title).HasMaxLength(160);

            // Tracks has no setter and returns a read-only wrapper, so EF must use _tracks.
            // EF finds the field by convention; spelled out here so the demo shows it.
            album.HasMany(a => a.Tracks).WithOne().HasForeignKey("AlbumId").IsRequired();
            album.Navigation(a => a.Tracks).HasField("_tracks").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        // Audit columns on every entity type, with no AuditableEntity base class in the domain.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            modelBuilder.Entity(entityType.ClrType).Property<DateTime>(CreatedAt);
            modelBuilder.Entity(entityType.ClrType).Property<DateTime>(LastUpdated);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampAuditColumns();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampAuditColumns();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void StampAuditColumns()
    {
        var now = _clock();
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Metadata.FindProperty(LastUpdated) is null) continue;

            if (entry.State == EntityState.Added)
            {
                entry.Property(CreatedAt).CurrentValue = now;
                entry.Property(LastUpdated).CurrentValue = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(LastUpdated).CurrentValue = now;
            }
        }
    }
}
