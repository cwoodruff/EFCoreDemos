using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace temporal_tables.TemporalDemo;

public class Employee
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Position { get; set; }
    public decimal Salary { get; set; }
}

// Temporal tables are a SQL Server feature, so this demo uses its own SQL Server
// database (TemporalTablesDemo) and never touches the shared Chinook database.
public class TemporalContext : DbContext
{
    private static readonly ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
    {
        builder
            .AddFilter(DbLoggerCategory.Database.Command.Name, LogLevel.Information)
            .AddConsole();
    });

    public const string ConnectionString =
        "Server=localhost,1433;Database=TemporalTablesDemo;User Id=sa;Password=8riwudeg!!;TrustServerCertificate=True";

    public DbSet<Employee> Employees => Set<Employee>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder
            .UseSqlServer(ConnectionString)
            .EnableSensitiveDataLogging()
            .UseLoggerFactory(loggerFactory);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(entity =>
        {
            // Keys are assigned by the app so a deleted row can be restored with its original Id.
            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Salary).HasPrecision(10, 2);

            // System-versioned temporal table. SQL Server maintains the period columns
            // (mapped as shadow properties) and copies old row versions to EmployeeHistory.
            entity.ToTable("Employees", tb => tb.IsTemporal(t =>
            {
                t.HasPeriodStart("ValidFrom");
                t.HasPeriodEnd("ValidTo");
                t.UseHistoryTable("EmployeeHistory");
            }));
        });
    }
}
