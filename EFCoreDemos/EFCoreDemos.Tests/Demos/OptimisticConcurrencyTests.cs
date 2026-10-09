extern alias optimistic_concurrency;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using optimistic_concurrency::optimistic_concurrency;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// optimistic-concurrency: Document.Version is a concurrency token bumped by BumpVersionInterceptor on every
    /// modified save. A save based on a stale Version affects 0 rows and throws DbUpdateConcurrencyException,
    /// which the demo resolves with a three-way merge.
    /// DocsContext only has a parameterless constructor, so each context is pointed at a temp database.
    /// </summary>
    public class OptimisticConcurrencyTests : IDisposable
    {
        private readonly TempSqliteFile _file = new();

        public OptimisticConcurrencyTests()
        {
            using var db = CreateContext();
            db.Database.EnsureCreated();
            db.Documents.Add(new Document { Title = "Original", Body = "Initial body", Version = 1 });
            db.SaveChanges();
        }

        public void Dispose() => _file.Dispose();

        private DocsContext CreateContext()
        {
            var db = new DocsContext();
            db.Database.SetConnectionString(_file.ConnectionString);
            return db;
        }

        private Document ReadDocument()
        {
            using var db = CreateContext();
            return db.Documents.AsNoTracking().Single(d => d.Id == 1);
        }

        [Fact]
        public void Interceptor_BumpsVersionOnEveryModifiedSave()
        {
            using var db = CreateContext();
            var doc = db.Documents.Single(d => d.Id == 1);

            doc.Title = "First edit";
            db.SaveChanges();
            Assert.Equal(2, doc.Version);

            doc.Title = "Second edit";
            db.SaveChanges();
            Assert.Equal(3, doc.Version);

            Assert.Equal(3, ReadDocument().Version);
        }

        [Fact]
        public void StaleVersion_ThrowsDbUpdateConcurrencyException_AndKeepsTheWinnersData()
        {
            using var ctxA = CreateContext();
            using var ctxB = CreateContext();
            var docA = ctxA.Documents.Single(d => d.Id == 1);
            var docB = ctxB.Documents.Single(d => d.Id == 1);

            docA.Title = "Edited by A";
            ctxA.SaveChanges();

            docB.Body = "Edited by B";
            var ex = Assert.Throws<DbUpdateConcurrencyException>(() => ctxB.SaveChanges());

            var entry = Assert.Single(ex.Entries);
            Assert.Same(docB, entry.Entity);
            Assert.Equal(1, entry.OriginalValues["Version"]);
            Assert.Equal(2, entry.GetDatabaseValues()!["Version"]);

            var stored = ReadDocument();
            Assert.Equal(2, stored.Version);
            Assert.Equal("Edited by A", stored.Title);
            Assert.Equal("Initial body", stored.Body);
        }

        [Fact]
        public void RetryWithoutRefreshing_StillFails_AndDoesNotDoubleBump()
        {
            using var ctxA = CreateContext();
            using var ctxB = CreateContext();
            var docA = ctxA.Documents.Single(d => d.Id == 1);
            var docB = ctxB.Documents.Single(d => d.Id == 1);

            docA.Title = "Edited by A";
            ctxA.SaveChanges();

            docB.Body = "Edited by B";
            Assert.Throws<DbUpdateConcurrencyException>(() => ctxB.SaveChanges());
            Assert.Throws<DbUpdateConcurrencyException>(() => ctxB.SaveChanges());

            // The interceptor derives the bump from the ORIGINAL value, so retries don't keep incrementing.
            Assert.Equal(2, docB.Version);
            Assert.Equal(1, ctxB.Entry(docB).Property(d => d.Version).OriginalValue);
        }

        [Fact]
        public void ThreeWayMerge_KeepsAsTitle_AppliesBsBody()
        {
            using var ctxA = CreateContext();
            using var ctxB = CreateContext();
            var docA = ctxA.Documents.Single(d => d.Id == 1);
            var docB = ctxB.Documents.Single(d => d.Id == 1);

            docA.Title = "Edited by A";
            ctxA.SaveChanges();

            docB.Body = "Edited by B";
            var ex = Assert.Throws<DbUpdateConcurrencyException>(() => ctxB.SaveChanges());

            var entry = ex.Entries.Single();
            var dbValues = entry.GetDatabaseValues()!;
            entry.OriginalValues.SetValues(dbValues);
            entry.CurrentValues["Body"] = "Edited by B";
            entry.CurrentValues["Title"] = dbValues["Title"];
            ctxB.SaveChanges();

            var final = ReadDocument();
            Assert.Equal(3, final.Version);
            Assert.Equal("Edited by A", final.Title);
            Assert.Equal("Edited by B", final.Body);
        }

        [Fact]
        public void ClientWins_RefreshingOnlyOriginalValues_OverwritesTheOtherEdit()
        {
            using var ctxA = CreateContext();
            using var ctxB = CreateContext();
            var docA = ctxA.Documents.Single(d => d.Id == 1);
            var docB = ctxB.Documents.Single(d => d.Id == 1);

            docA.Title = "Edited by A";
            ctxA.SaveChanges();

            docB.Body = "Edited by B";
            var ex = Assert.Throws<DbUpdateConcurrencyException>(() => ctxB.SaveChanges());

            var entry = ex.Entries.Single();
            entry.OriginalValues.SetValues(entry.GetDatabaseValues()!);
            ctxB.SaveChanges();

            var final = ReadDocument();
            Assert.Equal(3, final.Version);
            Assert.Equal("Original", final.Title); // B's stale Title wins over A's edit.
            Assert.Equal("Edited by B", final.Body);
        }
    }
}
