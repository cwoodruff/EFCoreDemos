extern alias complex_vs_owned_types;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using complex_vs_owned_types::complex_vs_owned_types.Model;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// complex-vs-owned-types: the same Address value object mapped as an owned type (hidden keys,
    /// identity) and as a complex type (pure value), plus EF 10's struct, optional and JSON complex types.
    /// Each test gets fresh SQLite files.
    /// </summary>
    public class ComplexVsOwnedTypesTests : IDisposable
    {
        private readonly TempSqliteFile _ownedFile = new();
        private readonly TempSqliteFile _complexFile = new();
        private readonly SqlLog _log = new();

        private static readonly Address Seattle = new() { Street = "1 Pike St", City = "Seattle", PostCode = "98101", Country = "USA" };
        private static readonly Address Berlin = new() { Street = "Unter den Linden 5", City = "Berlin", PostCode = "10117", Country = "Germany" };

        public ComplexVsOwnedTypesTests()
        {
            using (var db = Owned())
            {
                db.Database.EnsureCreated();
            }

            using (var db = Complex())
            {
                db.Database.EnsureCreated();
            }
        }

        public void Dispose()
        {
            _ownedFile.Dispose();
            _complexFile.Dispose();
        }

        private OwnedContext Owned() => new(OwnedContext.CreateOptions(_ownedFile.ConnectionString, _log.Add));
        private ComplexContext Complex() => new(ComplexContext.CreateOptions(_complexFile.ConnectionString, _log.Add));

        private List<string> Query(string connectionString, string sql)
        {
            using var connection = new SqliteConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = command.ExecuteReader();
            var rows = new List<string>();
            while (reader.Read()) rows.Add(reader.GetString(0));
            return rows;
        }

        [Fact]
        public void Schema_OwnsManyNeedsATable_ComplexCollectionIsOneJsonColumn()
        {
            Assert.Equal(["Customers", "PreviousAddresses"],
                Query(_ownedFile.ConnectionString, "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;"));
            Assert.Equal(["Customers"],
                Query(_complexFile.ConnectionString, "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;"));

            var complexColumns = Query(_complexFile.ConnectionString, "SELECT name FROM pragma_table_info('Customers');");
            Assert.Contains("ShippingAddress_City", complexColumns);
            Assert.Contains("BillingAddress_City", complexColumns);
            Assert.Contains("Location_Latitude", complexColumns);
            Assert.Contains("PreviousAddresses", complexColumns);
        }

        [Fact]
        public void OwnedTypes_AreTrackedAsSeparateEntitiesWithHiddenKeys()
        {
            using (var db = Owned())
            {
                db.Customers.Add(new OwnedCustomer { Name = "Ada", ShippingAddress = Seattle, BillingAddress = Seattle with { }, PreviousAddresses = [Berlin] });
                db.SaveChanges();
            }

            using var read = Owned();
            read.Customers.Single();
            var entries = read.ChangeTracker.Entries().ToList();
            Assert.Equal(4, entries.Count); // customer + shipping + billing + one previous address
            Assert.All(entries.Where(e => e.Metadata.IsOwned()),
                e => Assert.All(e.Metadata.FindPrimaryKey()!.Properties, p => Assert.True(p.IsShadowProperty())));
        }

        [Fact]
        public void ComplexTypes_AreNotSeparateEntries()
        {
            using (var db = Complex())
            {
                db.Customers.Add(new ComplexCustomer { Name = "Ada", ShippingAddress = Seattle, BillingAddress = Seattle, PreviousAddresses = [Berlin] });
                db.SaveChanges();
            }

            using var read = Complex();
            read.Customers.Single();
            Assert.Single(read.ChangeTracker.Entries());
        }

        [Fact]
        public void SharingOneInstance_FailsForOwned_WorksForComplex()
        {
            var shared = new Address { Street = "9 Elm Rd", City = "Boise", PostCode = "83702", Country = "USA" };

            using (var db = Owned())
            {
                Assert.Throws<InvalidOperationException>(() =>
                    db.Customers.Add(new OwnedCustomer { Name = "Grace", ShippingAddress = shared, BillingAddress = shared }));
            }

            using (var db = Complex())
            {
                db.Customers.Add(new ComplexCustomer { Name = "Grace", ShippingAddress = shared, BillingAddress = shared });
                db.SaveChanges();
            }

            using var read = Complex();
            var grace = read.Customers.Single();
            Assert.Equal(shared, grace.ShippingAddress);
            Assert.Equal(shared, grace.BillingAddress);
        }

        [Fact]
        public void EqualityInLinq_ComparesValuesForComplex_IdentityForOwned()
        {
            using (var db = Owned())
            {
                db.Customers.Add(new OwnedCustomer { Name = "Grace", ShippingAddress = Seattle, BillingAddress = Seattle with { } });
                db.SaveChanges();
            }

            using (var db = Complex())
            {
                db.Customers.Add(new ComplexCustomer { Name = "Grace", ShippingAddress = Seattle, BillingAddress = Seattle });
                db.Customers.Add(new ComplexCustomer { Name = "Linus", ShippingAddress = Berlin, BillingAddress = Seattle });
                db.SaveChanges();
            }

            using (var db = Complex())
            {
                var query = db.Customers.Where(c => c.ShippingAddress == c.BillingAddress).Select(c => c.Name);
                Assert.Contains("\"ShippingAddress_City\" = \"c\".\"BillingAddress_City\"", query.ToQueryString());
                Assert.Equal(["Grace"], query.ToList());
                Assert.Equal(["Grace"], db.Customers.Where(c => c.ShippingAddress == Seattle).Select(c => c.Name).ToList());
            }

            using (var db = Owned())
            {
                // Identity comparison of two different owned entities: folded to false, so Grace is missed.
                var query = db.Customers.Where(c => c.ShippingAddress == c.BillingAddress).Select(c => c.Name);
                Assert.Contains("WHERE 0", query.ToQueryString());
                Assert.Empty(query.ToList());
            }
        }

        [Fact]
        public void ReplacingTheValue_OwnedIsDeleteAndAdd_ComplexIsAModifiedProperty()
        {
            using (var db = Owned())
            {
                db.Customers.Add(new OwnedCustomer { Name = "Ada", ShippingAddress = Seattle });
                db.SaveChanges();
            }

            using (var db = Complex())
            {
                db.Customers.Add(new ComplexCustomer { Name = "Ada", ShippingAddress = Seattle });
                db.SaveChanges();
            }

            using (var db = Owned())
            {
                var ada = db.Customers.Single();
                ada.ShippingAddress = Seattle with { Street = "400 Broad St" };
                db.ChangeTracker.DetectChanges();
                var states = db.ChangeTracker.Entries().Where(e => e.Metadata.IsOwned()).Select(e => e.State).Order().ToList();
                Assert.Equal([EntityState.Deleted, EntityState.Added], states);
                db.SaveChanges();
            }

            using (var db = Complex())
            {
                var ada = db.Customers.Single();
                ada.ShippingAddress = Seattle with { Street = "400 Broad St" };
                db.ChangeTracker.DetectChanges();
                Assert.Single(db.ChangeTracker.Entries());
                var modified = db.Entry(ada).ComplexProperty(c => c.ShippingAddress).Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name);
                Assert.Equal(["Street"], modified);
                db.SaveChanges();
            }

            using (var db = Owned()) Assert.Equal("400 Broad St", db.Customers.Single().ShippingAddress.Street);
            using (var db = Complex()) Assert.Equal("400 Broad St", db.Customers.Single().ShippingAddress.Street);
        }

        [Fact]
        public void ExecuteUpdate_CanSetAWholeComplexValue()
        {
            using (var db = Complex())
            {
                db.Customers.Add(new ComplexCustomer { Name = "Ada", ShippingAddress = Seattle });
                db.SaveChanges();
            }

            using (var db = Complex())
            {
                Assert.Equal(1, db.Customers.ExecuteUpdate(s => s.SetProperty(c => c.ShippingAddress, Berlin)));
            }

            using (var read = Complex()) Assert.Equal(Berlin, read.Customers.Single().ShippingAddress);
        }

        [Fact]
        public void Ef10_StructOptionalAndJsonComplexTypes_RoundTripAndQuery()
        {
            using (var db = Complex())
            {
                db.Customers.Add(new ComplexCustomer
                {
                    Name = "Ada", ShippingAddress = Seattle, BillingAddress = Seattle,
                    Location = new GeoPoint(47.6097, -122.3422), PreviousAddresses = [Berlin]
                });
                db.Customers.Add(new ComplexCustomer
                {
                    Name = "Linus", ShippingAddress = Berlin, BillingAddress = null,
                    Location = new GeoPoint(52.5170, 13.3889)
                });
                db.SaveChanges();
            }

            using var read = Complex();

            // Struct complex type
            Assert.Equal(new GeoPoint(52.5170, 13.3889), read.Customers.Single(c => c.Name == "Linus").Location);
            Assert.Equal(["Linus"], read.Customers.Where(c => c.Location.Latitude > 50).Select(c => c.Name).ToList());

            // Optional complex type
            Assert.Null(read.Customers.Single(c => c.Name == "Linus").BillingAddress);
            Assert.Equal(Seattle, read.Customers.Single(c => c.Name == "Ada").BillingAddress);
            Assert.Equal(["Linus"], read.Customers.Where(c => c.BillingAddress == null).Select(c => c.Name).ToList());

            // Complex collection mapped to JSON
            var raw = Assert.Single(Query(_complexFile.ConnectionString, "SELECT \"PreviousAddresses\" FROM \"Customers\" WHERE \"Name\" = 'Ada';"));
            Assert.StartsWith("[{", raw);
            Assert.Contains("\"City\":\"Berlin\"", raw);
            Assert.Equal(["Ada"], read.Customers.Where(c => c.PreviousAddresses.Any(a => a.City == "Berlin")).Select(c => c.Name).ToList());
            Assert.Equal([Berlin], read.Customers.Single(c => c.Name == "Ada").PreviousAddresses);
        }
    }
}
