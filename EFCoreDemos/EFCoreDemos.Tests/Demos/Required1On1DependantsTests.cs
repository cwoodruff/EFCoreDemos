extern alias required_1_on_1_dependants;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using required_1_on_1_dependants::required_1_on_1_dependants.RequiredDependents;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// required-1-on-1-dependants: Person owns two Address dependents split into the People table.
    /// HomeAddress is a required dependent (Navigation(...).IsRequired()), WorkAddress is optional.
    /// </summary>
    public class Required1On1DependantsTests : IDisposable
    {
        private readonly TempSqliteFile _temp = new();

        public Required1On1DependantsTests()
        {
            using var db = NewContext();
            db.Database.EnsureCreated();
        }

        public void Dispose() => _temp.Dispose();

        [Fact]
        public void Model_HomeAddressIsARequiredDependent_WorkAddressIsOptional()
        {
            using var db = NewContext();
            var person = db.Model.FindEntityType(typeof(Person))!;

            var home = person.FindNavigation(nameof(Person.HomeAddress))!;
            var work = person.FindNavigation(nameof(Person.WorkAddress))!;

            Assert.True(home.ForeignKey.IsOwnership);
            Assert.True(home.ForeignKey.IsRequiredDependent);
            Assert.False(work.ForeignKey.IsRequiredDependent);

            // Table splitting: both dependents live in the principal's table.
            Assert.Equal("People", home.TargetEntityType.GetTableName());
            Assert.Equal("People", work.TargetEntityType.GetTableName());
        }

        [Fact]
        public void Schema_OneTable_RequiredDependentColumnsAreNotNull_OptionalAreNullable()
        {
            Assert.Equal(["People"], Query("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';"));

            var notNull = Query("SELECT name || '=' || \"notnull\" FROM pragma_table_info('People');")
                .Select(s => s.Split('='))
                .ToDictionary(p => p[0], p => p[1] == "1");

            Assert.True(notNull["HomeAddress_Street"]);
            Assert.True(notNull["HomeAddress_City"]);
            Assert.False(notNull["HomeAddress_Postcode"]);
            Assert.False(notNull["WorkAddress_Street"]);
            Assert.False(notNull["WorkAddress_City"]);
            Assert.False(notNull["WorkAddress_Postcode"]);
        }

        [Fact]
        public void OptionalDependent_CanBeMissing_RequiredDependentIsAlwaysMaterialized()
        {
            using (var db = NewContext())
            {
                db.People.Add(new Person { Name = "Ada", HomeAddress = new Address { Street = "1 Analytical Way", City = "London", Postcode = "N1" } });
                db.People.Add(new Person
                {
                    Name = "Grace",
                    HomeAddress = new Address { Street = "7 Cobol Ct", City = "Arlington" },
                    WorkAddress = new Address { Street = "Navy Yard", City = "Washington", Postcode = "20374" }
                });
                db.SaveChanges();
            }

            using (var db = NewContext())
            {
                var people = db.People.OrderBy(p => p.Id).ToList();

                Assert.Equal("Ada", people[0].Name);
                Assert.Equal("London", people[0].HomeAddress.City);
                Assert.Null(people[0].WorkAddress);

                Assert.Equal("Arlington", people[1].HomeAddress.City);
                Assert.Null(people[1].HomeAddress.Postcode);
                Assert.Equal("Washington", people[1].WorkAddress.City);
            }
        }

        [Fact]
        public void SavingWithoutRequiredDependent_Fails()
        {
            using var db = NewContext();
            db.People.Add(new Person { Name = "Linus" });

            var ex = Assert.ThrowsAny<Exception>(() => db.SaveChanges());
            Assert.True(ex is InvalidOperationException or DbUpdateException, $"Unexpected {ex.GetType()}: {ex.Message}");

            using var verify = NewContext();
            Assert.False(verify.People.Any(p => p.Name == "Linus"));
        }

        [Fact]
        public void RemovingOptionalDependent_NullsItsColumns_RequiredDependentCanBeReplaced()
        {
            using (var db = NewContext())
            {
                db.People.Add(new Person
                {
                    Name = "Grace",
                    HomeAddress = new Address { Street = "7 Cobol Ct", City = "Arlington" },
                    WorkAddress = new Address { Street = "Navy Yard", City = "Washington", Postcode = "20374" }
                });
                db.SaveChanges();
            }

            using (var db = NewContext())
            {
                var grace = db.People.Single(p => p.Name == "Grace");
                grace.WorkAddress = null;
                grace.HomeAddress = new Address { Street = "1 New St", City = "Boston" };
                db.SaveChanges();
            }

            using (var db = NewContext())
            {
                var grace = db.People.Single(p => p.Name == "Grace");
                Assert.Null(grace.WorkAddress);
                Assert.Equal("1 New St", grace.HomeAddress.Street);
                Assert.Equal("Boston", grace.HomeAddress.City);
            }

            Assert.Equal("1", Assert.Single(Query("SELECT COUNT(*) FROM \"People\" WHERE \"WorkAddress_Street\" IS NULL AND \"WorkAddress_City\" IS NULL AND \"WorkAddress_Postcode\" IS NULL;")));
        }

        private PeopleContext NewContext()
        {
            var db = new PeopleContext();
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
            while (reader.Read()) result.Add(Convert.ToString(reader.GetValue(0))!);
            return result;
        }
    }
}
