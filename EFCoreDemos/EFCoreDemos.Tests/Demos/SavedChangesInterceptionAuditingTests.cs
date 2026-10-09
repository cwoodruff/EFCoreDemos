extern alias savedchanges_interception_auditing;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using savedchanges_interception_auditing::savedchanges_interception_auditing;
using Xunit;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// savedchanges-interception-auditing: AuditingInterceptor (an ISaveChangesInterceptor) writes a
    /// SaveChangesAudit row to a separate audit database before every save, and marks it succeeded or
    /// failed (with the error) afterwards.
    /// The demo's BlogsContext builds its interceptor from fixed paths in the output folder, so these tests attach
    /// the demo's AuditingInterceptor, Blog/Post entities and AuditContext to a context over temp databases.
    /// </summary>
    public class SavedChangesInterceptionAuditingTests : IDisposable
    {
        private readonly TempSqliteFile _blogs = new();
        private readonly TempSqliteFile _audit = new();

        public SavedChangesInterceptionAuditingTests()
        {
            using (var audit = new AuditContext(_audit.ConnectionString))
            {
                audit.Database.EnsureCreated();
            }

            using (var blogs = CreateContext())
            {
                blogs.Database.EnsureCreated();
            }
        }

        public void Dispose()
        {
            _blogs.Dispose();
            _audit.Dispose();
        }

        private AuditedBlogsContext CreateContext() =>
            new(_blogs.ConnectionString, new AuditingInterceptor(_audit.ConnectionString));

        private List<SaveChangesAudit> ReadAudits()
        {
            using var audit = new AuditContext(_audit.ConnectionString);
            return audit.SaveChangesAudits.Include(a => a.Entities).OrderBy(a => a.Id).ToList();
        }

        private void SeedBlog()
        {
            using var db = CreateContext();
            db.Add(new Blog { Name = "EF Blog", Posts = { new Post { Title = "EF Core 3.1!" }, new Post { Title = "EF Core 5.0!" } } });
            db.SaveChanges();
        }

        [Fact]
        public void Insert_WritesSucceededAuditWithOneEntryPerAddedEntity()
        {
            SeedBlog();

            var audit = Assert.Single(ReadAudits());
            Assert.True(audit.Succeeded);
            Assert.NotEqual(Guid.Empty, audit.AuditId);
            Assert.True(audit.EndTime >= audit.StartTime);
            Assert.Null(audit.ErrorMessage);

            Assert.Equal(3, audit.Entities.Count);
            Assert.All(audit.Entities, e => Assert.Equal(EntityState.Added, e.State));
            Assert.Single(audit.Entities, e => e.AuditMessage.StartsWith("Inserting Blog with ") && e.AuditMessage.Contains("Name: 'EF Blog'"));
            Assert.Equal(2, audit.Entities.Count(e => e.AuditMessage.StartsWith("Inserting Post with ")));
        }

        [Fact]
        public void UpdateDeleteAndInsert_AreEachAudited()
        {
            SeedBlog();

            using (var db = CreateContext())
            {
                var blog = db.Blogs.Include(b => b.Posts).Single();
                blog.Name = "EF Core Blog";
                db.Remove(blog.Posts.First());
                blog.Posts.Add(new Post { Title = "EF Core 6.0!" });
                db.SaveChanges();
            }

            var audits = ReadAudits();
            Assert.Equal(2, audits.Count);
            var audit = audits[1];
            Assert.True(audit.Succeeded);

            Assert.Equal(3, audit.Entities.Count);
            var modified = Assert.Single(audit.Entities, e => e.State == EntityState.Modified);
            Assert.StartsWith("Updating Blog with ", modified.AuditMessage);
            Assert.Contains("Name: 'EF Core Blog'", modified.AuditMessage);

            var deleted = Assert.Single(audit.Entities, e => e.State == EntityState.Deleted);
            Assert.StartsWith("Deleting Post with Id: ", deleted.AuditMessage);

            var added = Assert.Single(audit.Entities, e => e.State == EntityState.Added);
            Assert.Contains("Title: 'EF Core 6.0!'", added.AuditMessage);
        }

        [Fact]
        public async Task FailedSave_IsAuditedAsNotSucceeded_WithTheError()
        {
            SeedBlog();

            int existingPostId;
            using (var db = CreateContext())
            {
                existingPostId = db.Set<Post>().Select(p => p.Id).First();
            }

            using (var db = CreateContext())
            {
                db.Add(new Post { Id = existingPostId, Title = "Duplicate key" });
                await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            }

            var audits = ReadAudits();
            Assert.Equal(2, audits.Count);
            var failed = audits[1];
            Assert.False(failed.Succeeded);
            Assert.Contains("UNIQUE constraint failed", failed.ErrorMessage);
            Assert.True(failed.EndTime >= failed.StartTime);
            Assert.Single(failed.Entities, e => e.State == EntityState.Added);
        }

        [Fact]
        public void AuditIsWrittenToTheSeparateAuditDatabase_NotTheBlogsDatabase()
        {
            SeedBlog();

            using var blogs = CreateContext();
            Assert.Single(blogs.Blogs);
            Assert.Equal(2, blogs.Set<Post>().Count());
            Assert.Single(ReadAudits());
        }

        /// <summary>Same model as the demo's BlogsContext, but over temp databases.</summary>
        private sealed class AuditedBlogsContext(string connectionString, AuditingInterceptor interceptor) : DbContext
        {
            public DbSet<Blog> Blogs { get; set; } = null!;

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
                optionsBuilder.UseSqlite(connectionString).AddInterceptors(interceptor);
        }
    }
}
