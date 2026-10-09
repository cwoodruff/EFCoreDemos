using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace json_columns.JsonDemo;

// A customer whose contact information is stored as a single JSON document column.
public class Customer
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required ContactDetails Contact { get; set; }
}

// Complex types: no key, no table of their own. They live inside the Customer row as JSON.
public class ContactDetails
{
    public required string Email { get; set; }
    public required Address Address { get; set; }
    public List<PhoneNumber> Phones { get; set; } = [];
}

public class Address
{
    public required string Street { get; set; }
    public required string City { get; set; }
    public required string Country { get; set; }
    public required string PostCode { get; set; }
}

public class PhoneNumber
{
    public required string Kind { get; set; }
    public required string Number { get; set; }
}

// Uses its own database file so the shared Chinook schema and data are never touched.
public class JsonColumnsContext : DbContext
{
    private static readonly ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
    {
        builder
            .AddFilter(DbLoggerCategory.Database.Command.Name, LogLevel.Information)
            .AddConsole();
    });

    public static readonly string DatabasePath = Path.Combine(AppContext.BaseDirectory, "json-columns.db");

    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlite($"Data Source={DatabasePath}")
            .EnableSensitiveDataLogging()
            .UseLoggerFactory(loggerFactory);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            // The whole ContactDetails graph (including the nested Address and the
            // Phones collection) is serialized into one TEXT column named "Contact".
            entity.ComplexProperty(c => c.Contact, contact =>
            {
                contact.ToJson();
                contact.ComplexProperty(cd => cd.Address);
                contact.ComplexCollection(cd => cd.Phones);
            });
        });
    }
}
