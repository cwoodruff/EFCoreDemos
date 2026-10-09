using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using required_1_on_1_dependants.RequiredDependents;

namespace required_1_on_1_dependants;

// Required one-to-one dependents (EF Core 5+): Navigation(...).IsRequired() on the
// principal's navigation to an owned/table-split dependent.
// (The Chinook folder is kept only for the solution's database health tests.)
class Program
{
    static void Main()
    {
        using (var db = new PeopleContext())
        {
            db.Database.EnsureDeleted();
            db.Database.EnsureCreated();

            Section("1. Schema: one table, required vs optional dependent columns");
            Console.WriteLine(db.Database.GenerateCreateScript().Trim());
            Console.WriteLine("  -> HomeAddress_Street/City are NOT NULL (required dependent).");
            Console.WriteLine("  -> WorkAddress_* are all NULL-able: the whole WorkAddress may be missing.");
        }

        PeopleContext.LogSql = true;

        Section("2. Save: optional dependent may be null, required one may not");
        using (var db = new PeopleContext())
        {
            db.People.Add(new Person
            {
                Name = "Ada",
                HomeAddress = new Address { Street = "1 Analytical Way", City = "London", Postcode = "N1" }
                // no WorkAddress -> WorkAddress_* columns are written as NULL
            });
            db.People.Add(new Person
            {
                Name = "Grace",
                HomeAddress = new Address { Street = "7 Cobol Ct", City = "Arlington" },
                WorkAddress = new Address { Street = "Navy Yard", City = "Washington", Postcode = "20374" }
            });
            db.SaveChanges();
        }

        using (var db = new PeopleContext())
        {
            db.People.Add(new Person { Name = "Linus" }); // no HomeAddress!
            try
            {
                db.SaveChanges();
                Console.WriteLine("  (unexpected) saved a Person without a HomeAddress");
            }
            catch (Exception ex) when (ex is InvalidOperationException or DbUpdateException)
            {
                Console.WriteLine("  Saving 'Linus' without a HomeAddress failed:");
                Console.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException is not null)
                    Console.WriteLine($"  -> {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
                Console.WriteLine("  EF sends the INSERT without HomeAddress columns; the NOT NULL columns that the");
                Console.WriteLine("  required dependent produced are what reject it. (Optional WorkAddress: no problem.)");
            }
        }

        Section("3. Query: one SELECT, no JOIN; required dependent is always materialized");
        using (var db = new PeopleContext())
        {
            foreach (var p in db.People.OrderBy(p => p.Id).ToList())
            {
                var work = p.WorkAddress is null ? "null" : $"{p.WorkAddress.Street}, {p.WorkAddress.City}";
                Console.WriteLine($"  {p.Name,-6} Home: {p.HomeAddress.Street}, {p.HomeAddress.City}  |  Work: {work}");
            }
        }

        Section("4. Removing a dependent: optional -> NULLs, required -> must be replaced");
        using (var db = new PeopleContext())
        {
            var grace = db.People.Single(p => p.Name == "Grace");
            grace.WorkAddress = null;                     // fine: columns set to NULL
            grace.HomeAddress = new Address { Street = "1 New St", City = "Boston" }; // replace, not remove
            db.SaveChanges();
        }

        PeopleContext.LogSql = false;
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"=== {title} ===");
    }
}
