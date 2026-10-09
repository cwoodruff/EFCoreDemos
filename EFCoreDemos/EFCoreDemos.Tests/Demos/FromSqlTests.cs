extern alias from_sql;

using EFCoreDemos.Tests.Infrastructure;
using from_sql::from_sql.Chinook;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// from_sql: FromSqlInterpolated / FromSql turn interpolated values (including an explicit SqliteParameter)
    /// into DbParameters, so injection text is treated as data, and the raw SQL composes with LINQ operators
    /// (EF wraps it in a subquery).
    /// </summary>
    public class FromSqlTests : IDisposable
    {
        private const int CustomerCount = 59;
        private const int CanadaCustomerCount = 8;

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext(SqlLog? log = null) => new(_db.Options<ChinookContext>(log));

        [Fact]
        public void FromSqlInterpolated_WithExplicitSqliteParameter_ReturnsMatchingCustomers()
        {
            var log = new SqlLog();
            using var db = CreateContext(log);

            // Exactly what the demo does.
            var country = "Canada";
            var parameter = new SqliteParameter("Country", SqliteType.Text) { Size = country.Length, Value = country };

            var customers = db.Customers.FromSqlInterpolated($"SELECT * FROM [Customer] WHERE Country = {parameter}").ToList();

            Assert.Equal(CanadaCustomerCount, customers.Count);
            Assert.All(customers, c => Assert.Equal("Canada", c.Country));
            var command = Assert.Single(log.Commands);
            Assert.Contains("@Country", command);
            Assert.DoesNotContain("'Canada'", command);
        }

        [Fact]
        public void FromSql_InterpolatedValue_IsSentAsAParameter()
        {
            var log = new SqlLog();
            using var db = CreateContext(log);

            var country = "Canada";
            var customers = db.Customers.FromSql($"SELECT * FROM [Customer] WHERE Country = {country}").ToList();

            Assert.Equal(CanadaCustomerCount, customers.Count);
            var command = Assert.Single(log.Commands);
            Assert.DoesNotContain("Canada", command); // sensitive data logging is off, the value is only in a parameter
            Assert.Contains("@p0", command);
        }

        [Fact]
        public void FromSql_InjectionAttempt_IsTreatedAsData()
        {
            using var db = CreateContext();

            var malicious = "Canada' OR '1'='1";
            var safe = db.Customers.FromSql($"SELECT * FROM [Customer] WHERE Country = {malicious}").ToList();

            Assert.Empty(safe);
            Assert.Equal(CustomerCount, db.Customers.Count()); // nothing else was affected
        }

        [Fact]
        public void FromSqlRaw_WithConcatenation_IsInjectable_ForContrast()
        {
            using var db = CreateContext();

            var malicious = "Canada' OR '1'='1";
#pragma warning disable EF1003 // deliberately unsafe: shows what FromSql/FromSqlInterpolated protect against
            var unsafeResult = db.Customers.FromSqlRaw("SELECT * FROM [Customer] WHERE Country = '" + malicious + "'").ToList();
#pragma warning restore EF1003

            Assert.Equal(CustomerCount, unsafeResult.Count);
        }

        [Fact]
        public void FromSql_ComposesWithLinq_InASingleCommand()
        {
            var log = new SqlLog();
            using var db = CreateContext(log);

            var country = "Canada";
            var torontoNames = db.Customers
                .FromSql($"SELECT * FROM [Customer] WHERE Country = {country}")
                .Where(c => c.City == "Toronto")
                .OrderBy(c => c.LastName)
                .Select(c => c.FirstName + " " + c.LastName)
                .ToList();

            Assert.Single(torontoNames);

            var command = Assert.Single(log.Commands);
            Assert.Contains("SELECT * FROM [Customer] WHERE Country = @p0", command);
            Assert.Contains("FROM (", command);
            Assert.Contains("\"City\" = 'Toronto'", command);
            Assert.Contains("ORDER BY", command);
        }

        [Fact]
        public void FromSql_ComposesWithIncludeAndAggregates()
        {
            using var db = CreateContext();

            var country = "Canada";
            var viaSql = db.Customers
                .FromSql($"SELECT * FROM [Customer] WHERE Country = {country}")
                .Include(c => c.Invoices)
                .AsNoTracking()
                .ToList();
            var viaLinq = db.Customers.Where(c => c.Country == country).Include(c => c.Invoices).AsNoTracking().ToList();

            Assert.Equal(
                viaLinq.OrderBy(c => c.Id).Select(c => (c.Id, c.Invoices.Count)),
                viaSql.OrderBy(c => c.Id).Select(c => (c.Id, c.Invoices.Count)));
            Assert.Equal(CanadaCustomerCount, db.Customers.FromSql($"SELECT * FROM [Customer] WHERE Country = {country}").Count());
        }
    }
}
