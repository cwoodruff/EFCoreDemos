using complex_vs_owned_types.Model;
using Microsoft.EntityFrameworkCore;

namespace complex_vs_owned_types;

internal class Program
{
    // Each model gets its own throwaway SQLite file next to the binaries.
    private static readonly string OwnedConnection = $"Data Source={Path.Combine(AppContext.BaseDirectory, "owned.db")}";
    private static readonly string ComplexConnection = $"Data Source={Path.Combine(AppContext.BaseDirectory, "complex.db")}";

    // SQL logging is switched on only for the steps where the SQL is the point.
    private static bool _showSql;

    private static void Log(string message)
    {
        if (_showSql) Console.WriteLine(message);
    }

    private static OwnedContext Owned() => new(OwnedContext.CreateOptions(OwnedConnection, Log));
    private static ComplexContext Complex() => new(ComplexContext.CreateOptions(ComplexConnection, Log));

    private static readonly Address Seattle = new() { Street = "1 Pike St", City = "Seattle", PostCode = "98101", Country = "USA" };
    private static readonly Address Portland = new() { Street = "200 Burnside St", City = "Portland", PostCode = "97204", Country = "USA" };
    private static readonly Address Berlin = new() { Street = "Unter den Linden 5", City = "Berlin", PostCode = "10117", Country = "Germany" };

    public static async Task Main()
    {
        // ------------------------------------------------------------------
        Header("1. Same Address value object, two mappings: look at the tables");
        await using (var db = Owned())
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            Console.WriteLine("-- OWNED (OwnsOne x2 + OwnsMany)");
            Console.WriteLine(db.Database.GenerateCreateScript());
        }

        await using (var db = Complex())
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
            Console.WriteLine("-- COMPLEX (ComplexProperty x3 + ComplexCollection.ToJson())");
            Console.WriteLine(db.Database.GenerateCreateScript());
        }

        Console.WriteLine("The single-address columns look the same (ShippingAddress_City, ...).");
        Console.WriteLine("The collection does not: OwnsMany needs a second table with its own hidden key");
        Console.WriteLine("(\"Id\") and a foreign key; ComplexCollection is one JSON column in the Customers row.");

        await using (var db = Owned())
        {
            db.Customers.Add(new OwnedCustomer
            {
                Name = "Ada",
                ShippingAddress = Seattle,
                BillingAddress = Portland,
                PreviousAddresses = [Berlin]
            });
            await db.SaveChangesAsync();
        }

        await using (var db = Complex())
        {
            db.Customers.Add(new ComplexCustomer
            {
                Name = "Ada",
                ShippingAddress = Seattle,
                BillingAddress = Portland,
                Location = new GeoPoint(47.6097, -122.3422),
                PreviousAddresses = [Berlin]
            });
            await db.SaveChangesAsync();
        }

        // ------------------------------------------------------------------
        Header("2. Identity: owned types are entities with hidden keys");
        await using (var db = Owned())
        {
            var ada = await db.Customers.SingleAsync(c => c.Name == "Ada");
            Console.WriteLine($"Owned model - change tracker entries for one customer: {db.ChangeTracker.Entries().Count()}");
            foreach (var entry in db.ChangeTracker.Entries())
            {
                var key = entry.Metadata.FindPrimaryKey()!.Properties
                    .Select(p => $"{p.Name}{(p.IsShadowProperty() ? " (shadow)" : "")}={entry.Property(p.Name).CurrentValue}");
                Console.WriteLine($"  {entry.Metadata.DisplayName(),-45} key: {string.Join(", ", key)}");
            }
        }

        await using (var db = Complex())
        {
            var ada = await db.Customers.SingleAsync(c => c.Name == "Ada");
            Console.WriteLine();
            Console.WriteLine($"Complex model - change tracker entries for one customer: {db.ChangeTracker.Entries().Count()}");
            var entry = db.Entry(ada);
            Console.WriteLine($"  {entry.Metadata.DisplayName(),-45} key: Id={ada.Id}");
            Console.WriteLine($"  ShippingAddress is part of that entry: {entry.ComplexProperty(c => c.ShippingAddress).Property(a => a.City).CurrentValue}");
        }

        // ------------------------------------------------------------------
        Header("3. Assign the SAME Address instance to shipping and billing");
        await using (var db = Owned())
        {
            var shared = new Address { Street = "9 Elm Rd", City = "Boise", PostCode = "83702", Country = "USA" };
            try
            {
                // Throws as soon as EF tries to track the second owned entity: the instance
                // is already tracked as ShippingAddress.
                db.Customers.Add(new OwnedCustomer { Name = "Grace", ShippingAddress = shared, BillingAddress = shared });
                await db.SaveChangesAsync();
                Console.WriteLine("Owned: saved (unexpected).");
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine("Owned:   InvalidOperationException");
                Console.WriteLine($"         {FirstSentence(ex.Message)}");
                Console.WriteLine("         One instance can't be two owned entities - each needs its own identity.");
            }
        }

        await using (var db = Owned())
        {
            // The workaround: two separate (but equal) instances.
            var shipping = new Address { Street = "9 Elm Rd", City = "Boise", PostCode = "83702", Country = "USA" };
            db.Customers.Add(new OwnedCustomer { Name = "Grace", ShippingAddress = shipping, BillingAddress = shipping with { } });
            await db.SaveChangesAsync();
            Console.WriteLine("Owned:   saved only after copying the value into a second instance.");
        }

        await using (var db = Complex())
        {
            var shared = new Address { Street = "9 Elm Rd", City = "Boise", PostCode = "83702", Country = "USA" };
            db.Customers.Add(new ComplexCustomer { Name = "Grace", ShippingAddress = shared, BillingAddress = shared });
            await db.SaveChangesAsync();
            Console.WriteLine("Complex: saved. A value is just a value - the columns are copied into both slots.");
        }

        // ------------------------------------------------------------------
        Header("4. Compare whole values in a query: shipping == billing");
        await using (var db = Complex())
        {
            _showSql = true;
            var sameAddress = await db.Customers
                .Where(c => c.ShippingAddress == c.BillingAddress)
                .Select(c => c.Name)
                .ToListAsync();
            var inSeattle = await db.Customers
                .Where(c => c.ShippingAddress == Seattle)
                .Select(c => c.Name)
                .ToListAsync();
            _showSql = false;
            Console.WriteLine($"Complex: shipping == billing -> [{string.Join(", ", sameAddress)}]");
            Console.WriteLine($"Complex: shipping == Seattle -> [{string.Join(", ", inSeattle)}]");
            Console.WriteLine("EF expands the comparison column by column - no hand-written member list.");
        }

        await using (var db = Owned())
        {
            // Same LINQ against the owned model. It translates - but == on an entity means
            // "same identity", not "same address values".
            Console.WriteLine();
            var query = db.Customers
                .Where(c => c.ShippingAddress == c.BillingAddress)
                .Select(c => c.Name);
            Console.WriteLine(query.ToQueryString());
            var sameAddress = await query.ToListAsync();
            Console.WriteLine($"Owned:   shipping == billing -> [{string.Join(", ", sameAddress)}]");
            Console.WriteLine("         WHERE 0: two different owned entities can never be the same identity, so EF");
            Console.WriteLine("         folds the predicate to false. Grace has identical values but is silently missed.");
            Console.WriteLine("         With owned types you must compare Street, City, ... yourself.");
        }

        // ------------------------------------------------------------------
        Header("5. Replace the whole value (moving house)");
        await using (var db = Owned())
        {
            var ada = await db.Customers.SingleAsync(c => c.Name == "Ada");
            ada.ShippingAddress = Seattle with { Street = "400 Broad St" };
            db.ChangeTracker.DetectChanges();
            Console.WriteLine("Owned - tracked states after assigning a new Address:");
            foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged))
            {
                Console.WriteLine($"  {entry.Metadata.DisplayName(),-45} {entry.State}");
            }

            _showSql = true;
            await db.SaveChangesAsync();
            _showSql = false;
            Console.WriteLine("The change tracker sees identity churn: the old owned entity Deleted, a new one Added.");
            Console.WriteLine("Because both live in the Customers row EF collapses that into one UPDATE - but with");
            Console.WriteLine("OwnsMany (its own table) replacing an item really is a DELETE + INSERT.");
        }

        await using (var db = Complex())
        {
            var ada = await db.Customers.SingleAsync(c => c.Name == "Ada");
            ada.ShippingAddress = Seattle with { Street = "400 Broad St" };
            db.ChangeTracker.DetectChanges();
            var modified = db.Entry(ada).ComplexProperty(c => c.ShippingAddress).Properties
                .Where(p => p.IsModified)
                .Select(p => p.Metadata.Name);
            Console.WriteLine();
            Console.WriteLine($"Complex - customer is {db.Entry(ada).State}; modified address members: {string.Join(", ", modified)}");

            _showSql = true;
            await db.SaveChangesAsync();
            _showSql = false;
            Console.WriteLine("EF compares the old and new value member by member and only writes what changed.");
        }

        // ------------------------------------------------------------------
        Header("6. EF 10 additions to complex types");
        await using (var db = Complex())
        {
            db.Customers.Add(new ComplexCustomer
            {
                Name = "Linus",
                ShippingAddress = Berlin,
                BillingAddress = null,
                Location = new GeoPoint(52.5170, 13.3889)
            });
            await db.SaveChangesAsync();
        }

        await using (var db = Complex())
        {
            Console.WriteLine("a) Struct complex type (GeoPoint is a readonly record struct):");
            _showSql = true;
            var north = await db.Customers
                .Where(c => c.Location.Latitude > 50)
                .Select(c => new { c.Name, c.Location })
                .ToListAsync();
            _showSql = false;
            foreach (var c in north) Console.WriteLine($"   {c.Name}: {c.Location}");

            Console.WriteLine();
            Console.WriteLine("b) Optional complex type (BillingAddress is Address?):");
            var linus = await db.Customers.SingleAsync(c => c.Name == "Linus");
            Console.WriteLine($"   Linus.BillingAddress round-tripped as: {linus.BillingAddress?.ToString() ?? "null"}");
            var noBilling = await db.Customers.Where(c => c.BillingAddress == null).Select(c => c.Name).ToListAsync();
            Console.WriteLine($"   Customers WHERE BillingAddress == null: [{string.Join(", ", noBilling)}]");

            Console.WriteLine();
            Console.WriteLine("c) Complex collection mapped to JSON (PreviousAddresses):");
            var raw = await db.Database
                .SqlQuery<string>($"SELECT \"PreviousAddresses\" AS \"Value\" FROM \"Customers\" WHERE \"Name\" = 'Ada'")
                .SingleAsync();
            Console.WriteLine($"   Raw column: {raw}");
            _showSql = true;
            var livedInBerlin = await db.Customers
                .Where(c => c.PreviousAddresses.Any(a => a.City == "Berlin"))
                .Select(c => c.Name)
                .ToListAsync();
            _showSql = false;
            Console.WriteLine($"   Previously lived in Berlin: [{string.Join(", ", livedInBerlin)}]");
        }

        // ------------------------------------------------------------------
        Header("7. When to choose which");
        Console.WriteLine("""
          Complex type (default for value objects on EF 10)
            + No identity: share, copy, compare and replace values freely
            + Works with records and structs; one change-tracker entry per entity
            + == in LINQ compares every member; ExecuteUpdate can set the whole value
            + Optional (null) values and collections (as JSON) are supported in EF 10
            - Collections must be stored as JSON - no separate table

          Owned type (keep for existing models, or when you need what only an entity can do)
            + OwnsMany into a separate, relational table (queryable, indexable rows)
            + Can have navigations to other entities
            - Hidden keys, tracked as separate entities; one instance can't be reused
            - == in LINQ compares identity, not values (silently false here)
            - Replacing a value is tracked as delete + insert

          Rule of thumb: if it's "a value" (Address, Money, DateRange, GeoPoint), make it a
          complex type. If its rows need a life of their own in a table, it's an entity -
          consider a regular entity type before reaching for OwnsMany.
          """);
    }

    private static string FirstSentence(string message)
    {
        var end = message.IndexOf(". ", StringComparison.Ordinal);
        return end > 0 ? message[..(end + 1)] : message;
    }

    private static void Header(string text)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 78));
        Console.WriteLine(text);
        Console.WriteLine(new string('=', 78));
    }
}
