using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace required_1_on_1_dependants.RequiredDependents;

// A principal with two one-to-one dependents of the same type, both stored in the
// principal's table (owned types => table splitting):
//   HomeAddress - REQUIRED dependent: every Person must have one.
//   WorkAddress - OPTIONAL dependent: a Person may have none.
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; }

    public Address HomeAddress { get; set; }
    public Address WorkAddress { get; set; }
}

public class Address
{
    public string Street { get; set; }
    public string City { get; set; }
    public string Postcode { get; set; }
}

// Uses its own scratch SQLite database (recreated every run), so the shared Chinook
// data is never touched.
public class PeopleContext : DbContext
{
    public static readonly string DatabasePath =
        Path.Combine(AppContext.BaseDirectory, "required-dependents.db");

    // Flipped on by Program so the console only shows the SQL for the interesting steps.
    public static bool LogSql { get; set; }

    public DbSet<Person> People => Set<Person>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
            .UseSqlite($"Data Source={DatabasePath}")
            .EnableSensitiveDataLogging()
            .LogTo(
                Console.WriteLine,
                (eventId, _) => LogSql && (eventId.Id == RelationalEventId.CommandExecuted.Id
                                         || eventId.Id == RelationalEventId.CommandError.Id),
                DbContextLoggerOptions.None);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>(b =>
        {
            b.ToTable("People");
            b.Property(p => p.Name).IsRequired();

            b.OwnsOne(p => p.HomeAddress, ConfigureAddress);
            b.Navigation(p => p.HomeAddress).IsRequired();   // <-- the required 1:1 dependent

            b.OwnsOne(p => p.WorkAddress, ConfigureAddress); // optional is the default
        });
    }

    // Street and City are required *within* an Address. For the required HomeAddress
    // that becomes NOT NULL columns. For the optional WorkAddress EF must make the
    // columns nullable (a row with no WorkAddress stores NULLs) and it uses these
    // required properties to tell "no WorkAddress" apart from "WorkAddress exists".
    private static void ConfigureAddress(OwnedNavigationBuilder<Person, Address> address)
    {
        address.Property(a => a.Street).IsRequired();
        address.Property(a => a.City).IsRequired();
    }
}
