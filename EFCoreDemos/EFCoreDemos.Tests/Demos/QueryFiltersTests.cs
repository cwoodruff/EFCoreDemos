extern alias query_filters;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using query_filters::Demos.Chinook;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// query-filters: the global query filter on Invoice (InvoiceDate >= 2009-01-01) is applied to every query,
    /// including Include, and IgnoreQueryFilters removes it.
    /// </summary>
    public class QueryFiltersTests : IDisposable
    {
        // Seeded Chinook: 458 invoices, 246 dated 2009 or later (212 earlier); 59 customers, 57 of whom
        // have at least one invoice from 2009 onwards.
        private const int AllInvoices = 458;
        private const int FilteredInvoices = 246;
        private const int CustomerCount = 59;
        private const int CustomersWithFilteredInvoices = 57;

        private static readonly DateTime Cutoff = new(2009, 1, 1);

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext() => new(_db.Options<ChinookContext>());

        [Fact]
        public void QueryFilter_ExcludesInvoicesBefore2009()
        {
            using var db = CreateContext();

            var invoices = db.Invoices.ToList();

            Assert.Equal(FilteredInvoices, invoices.Count);
            Assert.All(invoices, i => Assert.True(i.InvoiceDate >= Cutoff, $"Invoice {i.Id} dated {i.InvoiceDate} leaked through."));
        }

        [Fact]
        public void IgnoreQueryFilters_RestoresAllInvoices()
        {
            using var db = CreateContext();

            Assert.Equal(AllInvoices, db.Invoices.IgnoreQueryFilters().Count());
            Assert.Equal(FilteredInvoices, db.Invoices.Count());
        }

        [Fact]
        public void QueryFilter_IsAppliedToIncludedCollections()
        {
            using var db = CreateContext();

            // The demo's query.
            var customers = db.Customers.Include(b => b.Invoices).ToList();

            // Customer itself is unfiltered; only its invoices are.
            Assert.Equal(CustomerCount, customers.Count);
            Assert.Equal(FilteredInvoices, customers.Sum(c => c.Invoices.Count));
            Assert.Equal(CustomersWithFilteredInvoices, customers.Count(c => c.Invoices.Count > 0));
            Assert.All(customers.SelectMany(c => c.Invoices), i => Assert.True(i.InvoiceDate >= Cutoff));
        }

        [Fact]
        public void IgnoreQueryFilters_AppliesToIncludedCollectionsToo()
        {
            using var db = CreateContext();

            var customers = db.Customers.Include(b => b.Invoices).IgnoreQueryFilters().ToList();

            Assert.Equal(CustomerCount, customers.Count);
            Assert.Equal(AllInvoices, customers.Sum(c => c.Invoices.Count));
        }

        [Fact]
        public void QueryFilter_AppearsInGeneratedSql()
        {
            using var db = CreateContext();

            var filteredSql = db.Invoices.ToQueryString();
            var unfilteredSql = db.Invoices.IgnoreQueryFilters().ToQueryString();

            Assert.Contains("WHERE", filteredSql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("2009-01-01", filteredSql);
            Assert.DoesNotContain("WHERE", unfilteredSql, StringComparison.OrdinalIgnoreCase);
        }
    }
}
