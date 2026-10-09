using json_columns.JsonDemo;
using Microsoft.EntityFrameworkCore;

namespace json_columns;

internal class Program
{
    public static async Task Main(string[] args)
    {
        // 1. Create a fresh database. Look at the CREATE TABLE: "Contact" is a single TEXT column.
        Header("1. Create the schema (Contact is one JSON column)");
        await using (var db = new JsonColumnsContext())
        {
            await db.Database.EnsureDeletedAsync();
            await db.Database.EnsureCreatedAsync();
        }

        // 2. Save customers. EF serializes the ContactDetails graph to JSON on INSERT.
        Header("2. Insert customers with nested JSON documents");
        await using (var db = new JsonColumnsContext())
        {
            db.Customers.AddRange(
                new Customer
                {
                    Name = "Luís Gonçalves",
                    Contact = new ContactDetails
                    {
                        Email = "luisg@embraer.com.br",
                        Address = new Address { Street = "Av. Brigadeiro Faria Lima, 2170", City = "São José dos Campos", Country = "Brazil", PostCode = "12227-000" },
                        Phones = [new PhoneNumber { Kind = "Work", Number = "+55 (12) 3923-5555" }]
                    }
                },
                new Customer
                {
                    Name = "Leonie Köhler",
                    Contact = new ContactDetails
                    {
                        Email = "leonekohler@surfeu.de",
                        Address = new Address { Street = "Theodor-Heuss-Straße 34", City = "Stuttgart", Country = "Germany", PostCode = "70174" },
                        Phones =
                        [
                            new PhoneNumber { Kind = "Work", Number = "+49 0711 2842222" },
                            new PhoneNumber { Kind = "Mobile", Number = "+49 0151 1234567" }
                        ]
                    }
                },
                new Customer
                {
                    Name = "Frank Harris",
                    Contact = new ContactDetails
                    {
                        Email = "fharris@google.com",
                        Address = new Address { Street = "1600 Amphitheatre Parkway", City = "Mountain View", Country = "USA", PostCode = "94043-1351" },
                        Phones = [new PhoneNumber { Kind = "Mobile", Number = "+1 (650) 253-0000" }]
                    }
                });

            await db.SaveChangesAsync();
        }

        // 3. Filter on a property INSIDE the JSON document -> json_extract / ->> in the WHERE clause.
        Header("3. Query into the JSON: customers whose Address.Country is Germany");
        await using (var db = new JsonColumnsContext())
        {
            var germans = await db.Customers
                .Where(c => c.Contact.Address.Country == "Germany")
                .ToListAsync();

            foreach (var c in germans)
            {
                Console.WriteLine($"  {c.Name} - {c.Contact.Address.City} ({c.Contact.Phones.Count} phone numbers)");
            }
        }

        // 4. Project only JSON properties: the full document is not materialized.
        Header("4. Project JSON properties (name, city, email)");
        await using (var db = new JsonColumnsContext())
        {
            var rows = await db.Customers
                .OrderBy(c => c.Contact.Address.City)
                .Select(c => new { c.Name, c.Contact.Address.City, c.Contact.Email })
                .ToListAsync();

            foreach (var r in rows)
            {
                Console.WriteLine($"  {r.City,-20} {r.Name,-16} {r.Email}");
            }
        }

        // 5. Query into a JSON collection -> json_each in a subquery.
        Header("5. Query into a JSON array: customers with a Mobile phone");
        await using (var db = new JsonColumnsContext())
        {
            var withMobile = await db.Customers
                .Where(c => c.Contact.Phones.Any(p => p.Kind == "Mobile"))
                .Select(c => c.Name)
                .ToListAsync();

            Console.WriteLine($"  {string.Join(", ", withMobile)}");
        }

        // 6. Change tracking: modify one property inside the document and SaveChanges.
        //    EF detects the change inside the complex type and writes the Contact column
        //    (check @p0 in the log: the full, re-serialized document).
        Header("6. Update one JSON property via change tracking + SaveChanges");
        await using (var db = new JsonColumnsContext())
        {
            var frank = await db.Customers.SingleAsync(c => c.Name == "Frank Harris");
            frank.Contact.Address.City = "Sunnyvale";
            await db.SaveChangesAsync();
        }

        // 7. Bulk update straight into the JSON column without loading entities.
        //    Here EF targets just the one JSON path with json_set.
        Header("7. ExecuteUpdate a single JSON path (json_set, no entities loaded)");
        await using (var db = new JsonColumnsContext())
        {
            var updated = await db.Customers
                .Where(c => c.Contact.Address.Country == "USA")
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Contact.Address.Country, "United States"));

            Console.WriteLine($"  Rows updated: {updated}");
        }

        // 8. Show the raw JSON stored in the column.
        Header("8. Raw JSON stored in the Contact column");
        await using (var db = new JsonColumnsContext())
        {
            var raw = await db.Database
                .SqlQuery<string>($"SELECT \"Contact\" AS \"Value\" FROM \"Customers\" WHERE \"Name\" = 'Frank Harris'")
                .SingleAsync();

            Console.WriteLine($"  {raw}");
        }
    }

    private static void Header(string text)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 70));
        Console.WriteLine(text);
        Console.WriteLine(new string('=', 70));
    }
}
