extern alias json_columns;

using EFCoreDemos.Tests.Infrastructure;
using json_columns::json_columns.JsonDemo;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// json-columns: Customer.Contact (with nested Address and a Phones collection) is a complex type
    /// mapped with ToJson(), so the whole graph lives in one JSON "Contact" column that can be
    /// filtered, projected and updated in place.
    /// </summary>
    public class JsonColumnsTests : IDisposable
    {
        private readonly TempSqliteFile _temp = new();

        public JsonColumnsTests()
        {
            using var db = NewContext();
            db.Database.EnsureCreated();
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
            db.SaveChanges();
        }

        public void Dispose() => _temp.Dispose();

        [Fact]
        public void Schema_StoresWholeContactGraphInOneColumn()
        {
            Assert.Equal(["Id", "Name", "Contact"], Query("SELECT name FROM pragma_table_info('Customers') ORDER BY cid;"));

            var raw = Assert.Single(Query("SELECT \"Contact\" FROM \"Customers\" WHERE \"Name\" = 'Leonie Köhler';"));
            Assert.StartsWith("{", raw);
            Assert.Contains("\"Country\":\"Germany\"", raw);
            Assert.Contains("\"Mobile\"", raw);
        }

        [Fact]
        public void ComplexJsonGraph_RoundTrips()
        {
            using var db = NewContext();
            var leonie = db.Customers.Single(c => c.Name == "Leonie Köhler");

            Assert.Equal("leonekohler@surfeu.de", leonie.Contact.Email);
            Assert.Equal("Stuttgart", leonie.Contact.Address.City);
            Assert.Equal("70174", leonie.Contact.Address.PostCode);
            Assert.Equal(["Work", "Mobile"], leonie.Contact.Phones.Select(p => p.Kind));
            Assert.Equal("+49 0151 1234567", leonie.Contact.Phones[1].Number);
        }

        [Fact]
        public void CanFilterAndProjectInsideTheJsonDocument()
        {
            using var db = NewContext();

            var germans = db.Customers.Where(c => c.Contact.Address.Country == "Germany").Select(c => c.Name).ToList();
            Assert.Equal(["Leonie Köhler"], germans);

            var rows = db.Customers
                .OrderBy(c => c.Contact.Address.City)
                .Select(c => new { c.Name, c.Contact.Address.City, c.Contact.Email })
                .ToList();
            Assert.Equal(["Mountain View", "Stuttgart", "São José dos Campos"], rows.Select(r => r.City));
            Assert.Equal("fharris@google.com", rows[0].Email);

            var withMobile = db.Customers
                .Where(c => c.Contact.Phones.Any(p => p.Kind == "Mobile"))
                .Select(c => c.Name)
                .OrderBy(n => n)
                .ToList();
            Assert.Equal(["Frank Harris", "Leonie Köhler"], withMobile);
        }

        [Fact]
        public void ChangeTracking_DetectsChangeInsideNestedComplexType()
        {
            using (var db = NewContext())
            {
                var frank = db.Customers.Single(c => c.Name == "Frank Harris");
                frank.Contact.Address.City = "Sunnyvale";
                Assert.Equal(1, db.SaveChanges());
            }

            using (var db = NewContext())
            {
                Assert.Equal("Sunnyvale", db.Customers.Single(c => c.Name == "Frank Harris").Contact.Address.City);
            }
        }

        [Fact]
        public void ExecuteUpdate_SetsASingleJsonPathWithoutLoadingEntities()
        {
            using (var db = NewContext())
            {
                var updated = db.Customers
                    .Where(c => c.Contact.Address.Country == "USA")
                    .ExecuteUpdate(s => s.SetProperty(c => c.Contact.Address.Country, "United States"));

                Assert.Equal(1, updated);
                Assert.Empty(db.ChangeTracker.Entries());
            }

            using (var db = NewContext())
            {
                var frank = db.Customers.Single(c => c.Name == "Frank Harris");
                Assert.Equal("United States", frank.Contact.Address.Country);
                Assert.Equal("Mountain View", frank.Contact.Address.City);
                Assert.Single(frank.Contact.Phones);
            }
        }

        private JsonColumnsContext NewContext()
        {
            var db = new JsonColumnsContext();
            db.Database.SetConnectionString(_temp.ConnectionString);
            return db;
        }

        private List<string> Query(string sql)
        {
            using var conn = new SqliteConnection(_temp.ConnectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            var result = new List<string>();
            while (reader.Read()) result.Add(reader.GetString(0));
            return result;
        }
    }
}
