using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace savedchanges_interception_auditing;

public class BlogsContext : DbContext
{
    private readonly AuditingInterceptor _auditingInterceptor =
        new AuditingInterceptor(
            @"Server=(localdb)\mssqllocaldb;Database=Demo5.Audit;Trusted_Connection=True;ConnectRetryCount=0");

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder
            .AddInterceptors(_auditingInterceptor)
            .UseSqlServer(
                @"Server=(localdb)\mssqllocaldb;Database=Demo5.Interceptor;Trusted_Connection=True;ConnectRetryCount=0");

    public DbSet<Blog> Blogs { get; set; }
}

public class Blog
{
    public int Id { get; set; }
    public string Name { get; set; }

    public ICollection<Post> Posts { get; } = new List<Post>();
}

public class Post
{
    public int Id { get; set; }
    public string Title { get; set; }

    public Blog Blog { get; set; }
}