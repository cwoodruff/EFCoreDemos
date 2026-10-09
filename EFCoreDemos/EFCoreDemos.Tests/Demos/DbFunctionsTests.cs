extern alias db_functions;

using db_functions::Demos.Chinook;
using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// db-functions: a static C# method marked [DbFunction] maps to the SQL Server scalar function
    /// dbo.ComputeInvoiceCount, so it is translated into SQL instead of running client side.
    /// </summary>
    public class DbFunctionsTests
    {
        private static ChinookContext CreateContext(SqlLog? log = null)
        {
            var builder = new DbContextOptionsBuilder<ChinookContext>()
                .UseSqlServer(SqlServerTestServer.ConnectionString + "Database=Chinook;");
            if (log != null)
            {
                builder.LogTo(log.Add, [DbLoggerCategory.Database.Command.Name], Microsoft.Extensions.Logging.LogLevel.Information);
            }

            return new ChinookContext(builder.Options);
        }

        [Fact]
        public void ComputeInvoiceCount_IsRegisteredAsDbFunctionInDboSchema()
        {
            using var db = new ChinookContext();
            var method = typeof(ChinookContext).GetMethod(nameof(ChinookContext.ComputeInvoiceCount))!;

            var function = db.Model.FindDbFunction(method);

            Assert.NotNull(function);
            Assert.Equal("dbo", function.Schema);
            Assert.Equal("ComputeInvoiceCount", function.Name);
            Assert.True(function.IsScalar);
        }

        [Fact]
        public void ComputeInvoiceCount_TranslatesToScalarFunctionCall()
        {
            using var db = CreateContext();

            var sql = db.Invoices.Select(i => ChinookContext.ComputeInvoiceCount(i.CustomerId)).ToQueryString();

            Assert.Contains("[dbo].[ComputeInvoiceCount]", sql);
        }

        [SqlServerFact]
        public async Task ComputeInvoiceCount_ReturnsSameCountsAsGroupBy()
        {
            await using var db = CreateContext();

            var viaFunction = await db.Customers
                .Select(c => new { c.Id, Count = ChinookContext.ComputeInvoiceCount(c.Id) })
                .ToDictionaryAsync(x => x.Id, x => x.Count);

            var viaGroupBy = await db.Invoices
                .GroupBy(i => i.CustomerId)
                .Select(g => new { CustomerId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CustomerId, x => x.Count);

            Assert.NotEmpty(viaFunction);
            foreach (var (customerId, count) in viaFunction)
            {
                Assert.Equal(viaGroupBy.GetValueOrDefault(customerId), count);
            }

            // Known Chinook values.
            Assert.Equal(8, viaFunction[1]);
            Assert.Equal(15, viaFunction[2]);
        }

        [SqlServerFact]
        public async Task ComputeInvoiceCount_RunsOnServer_WhenUsedInWhere()
        {
            var log = new SqlLog();
            await using var db = CreateContext(log);

            var busyCustomers = await db.Customers
                .Where(c => ChinookContext.ComputeInvoiceCount(c.Id) > 10)
                .Select(c => c.Id)
                .ToListAsync();

            var expected = await db.Invoices
                .GroupBy(i => i.CustomerId)
                .Where(g => g.Count() > 10)
                .Select(g => g.Key)
                .ToListAsync();

            Assert.Contains(2, busyCustomers);
            Assert.Equal(expected.Order(), busyCustomers.Order());
            Assert.Contains(log.Commands, c => c.Contains("[dbo].[ComputeInvoiceCount]") && c.Contains("WHERE"));
        }

        [Fact]
        public void ComputeInvoiceCount_ClientStub_IsNotTheRealImplementation()
        {
            // Called directly in C# the method is only a stub; the real work happens in SQL Server.
            Assert.Equal(0, ChinookContext.ComputeInvoiceCount(2));
        }
    }
}
