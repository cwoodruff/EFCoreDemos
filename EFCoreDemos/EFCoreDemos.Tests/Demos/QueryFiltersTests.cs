extern alias query_filters;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using query_filters::Demos.Chinook;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// query-filters: two EF Core 10 named query filters on Invoice - "Since2009" (InvoiceDate >= 2009-01-01) and
    /// "SalesRep" (customer's support rep == context.SalesRepId) - apply to every query, including Include.
    /// IgnoreQueryFilters(["name"]) switches one off; IgnoreQueryFilters() switches all off.
    /// </summary>
    public class QueryFiltersTests : IDisposable
    {
        // Seeded Chinook: 458 invoices, 246 dated 2009 or later (212 earlier); 59 customers, 57 of whom
        // have at least one invoice from 2009 onwards.
        private const int AllInvoices = 458;
        private const int FilteredInvoices = 246;
        private const int CustomerCount = 59;
        private const int CustomersWithFilteredInvoices = 57;

        // Support rep 3 (Jane Peacock) looks after 21 customers: 159 invoices in total, 85 from 2009 onwards,
        // spread over 19 customers.
        private const int SalesRepId = 3;
        private const int SalesRepInvoices = 159;
        private const int SalesRepFilteredInvoices = 85;
        private const int SalesRepCustomersWithFilteredInvoices = 19;

        private static readonly DateTime Cutoff = new(2009, 1, 1);

        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        // No SalesRepId: the "SalesRep" filter is on but matches every invoice.
        private ChinookContext CreateContext() => new(_db.Options<ChinookContext>());

        private ChinookContext CreateSalesRepContext() => new(_db.Options<ChinookContext>()) { SalesRepId = SalesRepId };

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

        [Fact]
        public void Model_DeclaresBothNamedFiltersOnInvoice()
        {
            using var db = CreateContext();

            var keys = db.Model.FindEntityType(typeof(Invoice))!.GetDeclaredQueryFilters().Select(f => f.Key).ToList();

            Assert.Equal([ChinookContext.Since2009Filter, ChinookContext.SalesRepFilter], keys);
        }

        [Fact]
        public void SalesRepFilter_LimitsInvoicesToOneRepsCustomers()
        {
            using var db = CreateSalesRepContext();

            var invoices = db.Invoices.Include(i => i.Customer).ToList();

            Assert.Equal(SalesRepFilteredInvoices, invoices.Count);
            Assert.All(invoices, i =>
            {
                Assert.Equal(SalesRepId, i.Customer.SupportRepId);
                Assert.True(i.InvoiceDate >= Cutoff);
            });
        }

        [Fact]
        public void IgnoringSince2009_KeepsTheSalesRepFilter()
        {
            using var db = CreateSalesRepContext();

            var invoices = db.Invoices.IgnoreQueryFilters([ChinookContext.Since2009Filter]).Include(i => i.Customer).ToList();

            Assert.Equal(SalesRepInvoices, invoices.Count);
            Assert.All(invoices, i => Assert.Equal(SalesRepId, i.Customer.SupportRepId));
            Assert.Contains(invoices, i => i.InvoiceDate < Cutoff);
        }

        [Fact]
        public void IgnoringSalesRep_KeepsTheSince2009Filter()
        {
            using var db = CreateSalesRepContext();

            var invoices = db.Invoices.IgnoreQueryFilters([ChinookContext.SalesRepFilter]).Include(i => i.Customer).ToList();

            Assert.Equal(FilteredInvoices, invoices.Count);
            Assert.All(invoices, i => Assert.True(i.InvoiceDate >= Cutoff));
            Assert.Contains(invoices, i => i.Customer.SupportRepId != SalesRepId);
        }

        [Fact]
        public void IgnoringBothByName_MatchesIgnoreQueryFilters()
        {
            using var db = CreateSalesRepContext();

            Assert.Equal(AllInvoices,
                db.Invoices.IgnoreQueryFilters([ChinookContext.Since2009Filter, ChinookContext.SalesRepFilter]).Count());
            Assert.Equal(AllInvoices, db.Invoices.IgnoreQueryFilters().Count());
        }

        [Fact]
        public void SalesRepFilter_IsAppliedToIncludedCollections()
        {
            using var db = CreateSalesRepContext();

            var customers = db.Customers.Include(c => c.Invoices).ToList();

            Assert.Equal(CustomerCount, customers.Count);
            Assert.Equal(SalesRepFilteredInvoices, customers.Sum(c => c.Invoices.Count));
            Assert.Equal(SalesRepCustomersWithFilteredInvoices, customers.Count(c => c.Invoices.Count > 0));
            Assert.All(customers.Where(c => c.Invoices.Count > 0), c => Assert.Equal(SalesRepId, c.SupportRepId));
        }

        [Fact]
        public void NamedFilters_EachAppearInGeneratedSqlUntilIgnored()
        {
            using var db = CreateSalesRepContext();

            var bothSql = db.Invoices.ToQueryString();
            var noDateSql = db.Invoices.IgnoreQueryFilters([ChinookContext.Since2009Filter]).ToQueryString();
            var noRepSql = db.Invoices.IgnoreQueryFilters([ChinookContext.SalesRepFilter]).ToQueryString();

            Assert.Contains("2009-01-01", bothSql);
            Assert.Contains("\"SupportRepId\"", bothSql);

            Assert.DoesNotContain("2009-01-01", noDateSql);
            Assert.Contains("\"SupportRepId\"", noDateSql);

            Assert.Contains("2009-01-01", noRepSql);
            Assert.DoesNotContain("SupportRepId", noRepSql);
            Assert.DoesNotContain("JOIN", noRepSql, StringComparison.OrdinalIgnoreCase);
        }
    }
}