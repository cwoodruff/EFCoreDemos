extern alias many_to_many;

using EFCoreDemos.Tests.Infrastructure;
using many_to_many::many_to_many;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// many-to-many: Post.Tags / Tag.Posts are skip navigations with no join entity class. EF creates an
    /// implicit property-bag join entity mapped to "PostTag" and maintains its rows automatically.
    /// </summary>
    public class ManyToManyTests : IDisposable
    {
        private readonly TempSqliteFile _temp = new();

        public ManyToManyTests()
        {
            // Same seed as the demo's SetupDatabase.
            using var db = NewContext();
            db.Database.EnsureCreated();

            var fishBlog = new Blog { Url = "http://sample.com/blogs/fish" };
            var fishPost = new Post { Title = "First Post!" };
            fishPost.Tags.Add(new Tag { Name = "Big Fish" });
            fishPost.Tags.Add(new Tag { Name = "Salt Water Fish" });

            var anotherFishPost = new Post { Title = "Second Post!" };
            anotherFishPost.Tags.Add(new Tag { Name = "Small Fish" });
            anotherFishPost.Tags.Add(new Tag { Name = "Fresh Water Fish" });

            fishBlog.Posts.Add(fishPost);
            fishBlog.Posts.Add(anotherFishPost);
            db.Blogs.Add(fishBlog);
            db.Blogs.Add(new Blog { Url = "http://sample.com/blogs/catfish" });
            db.Blogs.Add(new Blog { Url = "http://sample.com/blogs/cats" });
            db.SaveChanges();
        }

        public void Dispose() => _temp.Dispose();

        [Fact]
        public void SkipNavigations_ShareOneImplicitPropertyBagJoinEntity()
        {
            using var db = NewContext();

            var postTags = db.Model.FindEntityType(typeof(Post))!.FindSkipNavigation(nameof(Post.Tags))!;
            var tagPosts = db.Model.FindEntityType(typeof(Tag))!.FindSkipNavigation(nameof(Tag.Posts))!;

            Assert.Same(postTags.JoinEntityType, tagPosts.JoinEntityType);
            Assert.Same(tagPosts, postTags.Inverse);
            Assert.True(postTags.JoinEntityType.IsPropertyBag);
            Assert.Equal("PostTag", postTags.JoinEntityType.GetTableName());
        }

        [Fact]
        public void EnsureCreated_CreatesJoinTableWithBothForeignKeys()
        {
            var tables = Query("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;");
            Assert.Equal(["Blogs", "PostTag", "Posts", "Tags"], tables);

            var fkTargets = Query("SELECT \"table\" FROM pragma_foreign_key_list('PostTag') ORDER BY \"table\";");
            Assert.Equal(["Posts", "Tags"], fkTargets);

            Assert.Equal("4", Assert.Single(Query("SELECT COUNT(*) FROM \"PostTag\";")));
        }

        [Fact]
        public void ManyToMany_RoundTripsInBothDirections()
        {
            using var db = NewContext();

            var posts = db.Posts.Include(p => p.Tags).OrderBy(p => p.PostId).ToList();
            Assert.Equal(2, posts.Count);
            Assert.Equal(["Big Fish", "Salt Water Fish"], posts[0].Tags.Select(t => t.Name).OrderBy(n => n));
            Assert.Equal(["Fresh Water Fish", "Small Fish"], posts[1].Tags.Select(t => t.Name).OrderBy(n => n));

            var tag = db.Tags.Include(t => t.Posts).Single(t => t.Name == "Small Fish");
            Assert.Equal("Second Post!", Assert.Single(tag.Posts).Title);
        }

        [Fact]
        public void AddingAndRemovingThroughSkipNavigation_WritesJoinRows()
        {
            using (var db = NewContext())
            {
                var first = db.Posts.Include(p => p.Tags).Single(p => p.Title == "First Post!");
                var smallFish = db.Tags.Single(t => t.Name == "Small Fish");

                first.Tags.Remove(first.Tags.Single(t => t.Name == "Big Fish"));
                first.Tags.Add(smallFish); // existing tag now shared by two posts
                db.SaveChanges();
            }

            using (var db = NewContext())
            {
                var first = db.Posts.Include(p => p.Tags).Single(p => p.Title == "First Post!");
                Assert.Equal(["Salt Water Fish", "Small Fish"], first.Tags.Select(t => t.Name).OrderBy(n => n));
                Assert.Equal(2, db.Tags.Where(t => t.Name == "Small Fish").SelectMany(t => t.Posts).Count());

                // The removed link is gone, but the Tag itself still exists.
                Assert.True(db.Tags.Any(t => t.Name == "Big Fish"));
            }

            Assert.Equal("4", Assert.Single(Query("SELECT COUNT(*) FROM \"PostTag\";")));
        }

        [Fact]
        public void StringIdentityKey_IsGeneratedOnTheClientAsGuid()
        {
            using var db = NewContext();
            var tag = new Tag { Name = "Catfish" };

            db.Tags.Add(tag); // value generated on Add, before any INSERT

            Assert.True(Guid.TryParse(tag.TagId, out _), $"TagId '{tag.TagId}' is not a Guid string.");
            Assert.All(db.Tags.AsNoTracking().Select(t => t.TagId).ToList(), id => Assert.True(Guid.TryParse(id, out _)));
        }

        [Fact]
        public void BlogService_SearchBlogs_UsesLike()
        {
            using var db = NewContext();
            var urls = new BlogService(db).SearchBlogs("fish").Select(b => b.Url).OrderBy(u => u).ToList();

            Assert.Equal(["http://sample.com/blogs/catfish", "http://sample.com/blogs/fish"], urls);
        }

        private BloggingContext NewContext()
        {
            var db = new BloggingContext();
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
