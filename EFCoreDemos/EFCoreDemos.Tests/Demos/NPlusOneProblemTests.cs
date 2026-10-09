extern alias n_plus_one_problem;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using n_plus_one_problem::n_plus_one_problem.Chinook;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// n-plus-one-problem: querying invoices per customer in a loop issues 1 + N commands, while Include,
    /// a Select projection and a batched IN query each issue one or two commands and compute the same total.
    /// The queries below mirror the demo's Section A-D methods (which are private and only print).
    /// </summary>
    public class NPlusOneProblemTests : IDisposable
    {
        // Seeded Chinook: 59 customers, 458 invoices totalling 2799.38 (one customer has no invoices).
        private const int CustomerCount = 59;
        private const decimal GrandTotal = 2799.38m;

        private readonly ChinookCopy _db = new();
        private readonly SqlLog _log = new();

        public void Dispose() => _db.Dispose();

        /// <summary>
        /// The demo context only has a parameterless ctor and logs through a static LogSink; configuring the
        /// provider here makes the base OnConfiguring skip its own setup, so no static state is touched.
        /// </summary>
        private sealed class LoggedContext(string connectionString, SqlLog log) : ChinookContext
        {
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder
                    .UseSqlite(connectionString)
                    .LogTo(log.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information);
                base.OnConfiguring(optionsBuilder);
            }
        }

        private LoggedContext CreateContext() => new(_db.ConnectionString, _log);

        [Fact]
        public void SectionA_LoopQuery_IssuesNPlusOneCommands()
        {
            using var db = CreateContext();

            var customers = db.Customers.ToList();
            decimal grandTotal = 0m;
            foreach (var c in customers)
            {
                grandTotal += db.Invoices.Where(i => i.CustomerId == c.Id).Sum(i => i.Total);
            }

            Assert.Equal(CustomerCount, customers.Count);
            Assert.Equal(1 + CustomerCount, _log.Commands.Count);
            Assert.Equal(GrandTotal, grandTotal);
        }

        [Fact]
        public void SectionB_Include_IssuesOneCommand()
        {
            using var db = CreateContext();

            var customers = db.Customers.Include(c => c.Invoices).ToList();
            var grandTotal = customers.Sum(c => c.Invoices.Sum(i => i.Total));

            Assert.Equal(CustomerCount, customers.Count);
            Assert.Single(_log.Commands);
            Assert.Equal(GrandTotal, grandTotal);
        }

        [Fact]
        public void SectionC_Projection_IssuesOneCommand()
        {
            using var db = CreateContext();

            var rows = db.Customers
                .Select(c => new
                {
                    c.Id,
                    Name = c.FirstName + " " + c.LastName,
                    InvoiceTotal = c.Invoices.Sum(i => (decimal?)i.Total) ?? 0m
                })
                .ToList();

            Assert.Equal(CustomerCount, rows.Count);
            Assert.Single(_log.Commands);
            Assert.Equal(GrandTotal, rows.Sum(r => r.InvoiceTotal));
            Assert.Contains(rows, r => r.InvoiceTotal == 0m);
        }

        [Fact]
        public void SectionD_BatchedIn_IssuesTwoCommands()
        {
            using var db = CreateContext();

            var customers = db.Customers.AsNoTracking().ToList();
            var ids = customers.Select(c => c.Id).ToArray();

            var invoiceByCustomer = db.Invoices
                .Where(i => ids.Contains(i.CustomerId))
                .GroupBy(i => i.CustomerId)
                .Select(g => new { CustomerId = g.Key, Total = g.Sum(x => x.Total) })
                .ToLookup(x => x.CustomerId, x => x.Total);

            decimal grandTotal = 0m;
            foreach (var c in customers)
            {
                grandTotal += invoiceByCustomer[c.Id].FirstOrDefault();
            }

            Assert.Equal(2, _log.Commands.Count);
            Assert.Contains("GROUP BY", _log.Commands[1], StringComparison.OrdinalIgnoreCase);
            Assert.Equal(GrandTotal, grandTotal);
        }

        [Fact]
        public void AllApproaches_ComputeTheSamePerCustomerTotals()
        {
            using var db = CreateContext();

            var loop = db.Customers.AsNoTracking().OrderBy(c => c.Id).ToList()
                .ToDictionary(c => c.Id, c => db.Invoices.Where(i => i.CustomerId == c.Id).Sum(i => i.Total));
            var include = db.Customers.AsNoTracking().Include(c => c.Invoices).ToList()
                .ToDictionary(c => c.Id, c => c.Invoices.Sum(i => i.Total));
            var projected = db.Customers
                .Select(c => new { c.Id, Total = c.Invoices.Sum(i => (decimal?)i.Total) ?? 0m })
                .ToDictionary(x => x.Id, x => x.Total);

            Assert.Equal(CustomerCount, loop.Count);
            Assert.Equal(loop.OrderBy(kv => kv.Key), include.OrderBy(kv => kv.Key));
            Assert.Equal(loop.OrderBy(kv => kv.Key), projected.OrderBy(kv => kv.Key));
        }
    }
}
