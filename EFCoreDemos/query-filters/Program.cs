using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

#pragma warning disable 169

namespace Demos;

public class Program
{
    private static void Main()
    {
        SetupDatabase();

        using (var db = new BloggingContext())
        {
            var blogs = db.Blogs
                .Include(b => b.Posts)
                .ToList();

            foreach (var blog in blogs)
            {
                foreach (var post in blog.Posts)
                {
                    Console.WriteLine($" - {post.Title.PadRight(30)} [IsDeleted: {post.IsDeleted}]");
                }

                Console.WriteLine();
            }
        }

        Console.ReadLine();
    }

    private static void SetupDatabase()
    {
        using (var db = new BloggingContext())
        {
            if (db.Database.EnsureCreated())
            {
                db.Blogs.Add(
                    new Blog
                    {
                        Url = "http://sample.com/blogs/fish",
                        Posts = new List<Post>
                        {
                            new Post {Title = "Fish care 101", IsDeleted = false},
                            new Post {Title = "Caring for tropical fish", IsDeleted = false},
                            new Post {Title = "Types of ornamental fish", IsDeleted = true}
                        }
                    });

                db.Blogs.Add(
                    new Blog
                    {
                        Url = "http://sample.com/blogs/cats",
                        Posts = new List<Post>
                        {
                            new Post {Title = "Cat care 101", IsDeleted = true},
                            new Post {Title = "Caring for tropical cats", IsDeleted = false},
                            new Post {Title = "Food for cats", IsDeleted = false},
                            new Post {Title = "Toys cats", IsDeleted = true},
                            new Post {Title = "Grooming cats", IsDeleted = false},
                            new Post {Title = "The personalities of cats", IsDeleted = false},
                            new Post {Title = "Cats and dogs", IsDeleted = true}
                        }
                    });

                db.SaveChanges();
            }
        }
    }
}

public class BloggingContext : DbContext
{
    public BloggingContext()
    {
    }

    public DbSet<Blog> Blogs { get; set; }
    public DbSet<Post> Posts { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlServer(
                @"Server=(localdb)\mssqllocaldb;Database=QueryFilters;Trusted_Connection=True;ConnectRetryCount=0;")
            .UseLoggerFactory(loggerFactory);
    }
        
    static  readonly ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
    {
        builder
            .AddFilter(DbLoggerCategory.Database.Command.Name, LogLevel.Information)
            .AddConsole();
    });

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>();

        // Configure entity filters
        modelBuilder.Entity<Post>().HasQueryFilter(p => !p.IsDeleted);
    }
}

public class Blog
{
    public int BlogId { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public List<Post> Posts { get; set; }
}

public class Post
{
    public int PostId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public bool IsDeleted { get; set; }

    public int BlogId { get; set; }
    public Blog Blog { get; set; }
}