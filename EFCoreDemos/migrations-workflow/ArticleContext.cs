using Microsoft.EntityFrameworkCore;

namespace migrations_workflow;

public class Article
{
    public int Id { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string Isbn { get; set; }
}

public class ArticleContext : DbContext
{
    public const string ConnectionString = "Data Source=migrations.db";

    public DbSet<Article> Articles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite(ConnectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Article>(e =>
        {
            e.ToTable("Article");
            e.HasKey(a => a.Id);
            e.Property(a => a.Title).HasMaxLength(200).IsRequired();
            e.Property(a => a.Body).IsRequired();
            e.Property(a => a.Isbn).HasMaxLength(20);
            e.HasIndex(a => a.Isbn).IsUnique();
        });
    }
}
