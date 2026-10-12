using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace hierarchyid.Org;

/// <summary>
/// One row per Chinook employee, in a demo-owned database (Chinook itself is never altered).
/// The tree is stored twice on purpose: <see cref="Path"/> is the hierarchyid, <see cref="ManagerId"/>
/// is the classic adjacency list (Chinook's ReportsTo) that the recursive CTE walks.
/// </summary>
public class Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public int? ManagerId { get; set; }
    public HierarchyId Path { get; set; } = null!;
}

public class OrgContext(DbContextOptions<OrgContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    /// <summary>
    /// Options for the demo database. UseHierarchyId() registers the hierarchyid type mapping and the
    /// translations for IsDescendantOf, GetAncestor, GetLevel, GetReparentedValue, ...
    /// </summary>
    public static DbContextOptions<OrgContext> CreateOptions(string connectionString, Action<string>? log = null)
    {
        var builder = new DbContextOptionsBuilder<OrgContext>()
            .UseSqlServer(connectionString, sql => sql.UseHierarchyId());
        if (log != null)
        {
            builder.LogTo(log, [DbLoggerCategory.Database.Command.Name], LogLevel.Information);
        }

        return builder.Options;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(entity =>
        {
            // Keep Chinook's employee ids so the two databases are easy to compare.
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(50);
            entity.Property(e => e.Title).HasMaxLength(30);

            // hierarchyid sorts depth-first, so this unique index is also the "print the tree" index.
            entity.HasIndex(e => e.Path).IsUnique();

            entity.HasOne<Employee>()
                .WithMany()
                .HasForeignKey(e => e.ManagerId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }
}
