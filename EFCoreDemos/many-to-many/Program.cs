using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;


namespace many_to_many;

public class Program
{
    private static void Main()
    {
        SetupDatabase();

        using (var db = new BloggingContext())
        {
            var service = new BlogService(db);
            var blogs = service.SearchBlogs("fish");

            foreach (var blog in blogs)
            {
                Console.WriteLine(blog.Url);
            }
        }

        Console.ReadLine();
    }

    private static void SetupDatabase()
    {
        using (var db = new BloggingContext())
        {
            db.Database.EnsureDeleted();
                    
            if (db.Database.EnsureCreated())
            {
                var fishBlog = new Blog {Url = "http://sample.com/blogs/fish"};
                var fishPost = new Post {Title = "First Post!"};
                fishPost.Tags.Add(new Tag {Name = "Big Fish"});
                fishPost.Tags.Add(new Tag {Name = "Salt Water Fish"});
                    
                var anotherFishPost = new Post {Title = "Second Post!"};
                anotherFishPost.Tags.Add(new Tag {Name = "Small Fish"});
                anotherFishPost.Tags.Add(new Tag {Name = "Fresh Water Fish"});
                    
                fishBlog.Posts.Add(fishPost);
                fishBlog.Posts.Add(anotherFishPost);
                db.Blogs.Add(fishBlog);

                db.Blogs.Add(new Blog {Url = "http://sample.com/blogs/catfish"});
                db.Blogs.Add(new Blog {Url = "http://sample.com/blogs/cats"});

                db.SaveChanges();
            }
        }
    }
}

public class BloggingContext : DbContext
{
    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }
    public DbSet<Tag> Tags { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlServer(
                @"Server=(localdb)\mssqllocaldb;Database=ManyToMany;Trusted_Connection=True;ConnectRetryCount=0")
            .UseLoggerFactory(loggerFactory);
    }

    static readonly ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
    {
        builder
            .AddFilter(DbLoggerCategory.Database.Command.Name, LogLevel.Information)
            .AddConsole();
    });
        
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<Blog>()
            .HasMany(t => t.Posts);
                
        modelBuilder
            .Entity<Post>()
            .HasMany(p => p.Tags)
            .WithMany(t => t.Posts)
            .UsingEntity(j => j.ToTable("PostTag"));
            
        modelBuilder
            .Entity<Tag>()
            .HasMany(t => t.Posts)
            .WithMany(p => p.Tags)
            .UsingEntity(j => j.ToTable("PostTag"));
            
    }

    [DbFunction(Schema = "dbo")]
    public static int ComputePostCount(int blogId)
    {
        return 0;
    }
}

public class Blog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int BlogId { get; set; }
    public string Url { get; set; }

    public List<Post> Posts { get; set; } = new List<Post>();
}

public class Post
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }

    public int BlogId { get; set; }
    public Blog Blog { get; set; }
        
    public ICollection<Tag> Tags { get; set; } = new List<Tag>();
}
    
public class Tag
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public string TagId { get; set; }
    public string Name { get; set; }
        
    public ICollection<Post> Posts { get; set; } = new List<Post>();
}