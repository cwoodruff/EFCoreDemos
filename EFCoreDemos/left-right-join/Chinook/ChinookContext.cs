using Microsoft.EntityFrameworkCore;

namespace left_right_join.Chinook;

/// <summary>
/// Minimal Chinook model (SQLite) for the join demo. No navigation properties on purpose:
/// every relationship below is walked with an explicit join, which is what the demo is about.
/// </summary>
public class ChinookContext(DbContextOptions<ChinookContext> options) : DbContext(options)
{
    public DbSet<Artist> Artists => Set<Artist>();
    public DbSet<Album> Albums => Set<Album>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Artist>().ToTable("Artist");
        modelBuilder.Entity<Album>().ToTable("Album");
        modelBuilder.Entity<Customer>().ToTable("Customer");
        modelBuilder.Entity<Employee>().ToTable("Employee");
    }
}

public class Artist
{
    public int Id { get; set; }
    public string? Name { get; set; }
}

public class Album
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public int? ArtistId { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int? SupportRepId { get; set; }
}

public class Employee
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Title { get; set; }
}
