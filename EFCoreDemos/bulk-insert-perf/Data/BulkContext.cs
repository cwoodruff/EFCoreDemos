using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace bulk_insert_perf.Data;

/// <summary>Demo-owned database with a single OrderLines table. Chinook is never touched.</summary>
public class BulkContext(DbContextOptions<BulkContext> options) : DbContext(options)
{
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrderLine>(entity =>
        {
            entity.Property(e => e.OrderNumber).HasMaxLength(20);
            entity.Property(e => e.Sku).HasMaxLength(20);
            entity.Property(e => e.UnitPrice).HasPrecision(10, 2);
        });
    }

    /// <param name="maxBatchSize">Null keeps EF's default for SQL Server.</param>
    /// <param name="counter">Optional interceptor that counts the commands EF sends.</param>
    public static DbContextOptions<BulkContext> CreateOptions(
        string connectionString, int? maxBatchSize = null, CommandCounter? counter = null)
    {
        var builder = new DbContextOptionsBuilder<BulkContext>()
            .UseSqlServer(connectionString, sql =>
            {
                if (maxBatchSize is { } size) sql.MaxBatchSize(size);
            });

        if (counter != null) builder.AddInterceptors(counter);
        return builder.Options;
    }

    /// <summary>Creates the database and table if missing, then empties the table (and resets the IDENTITY).</summary>
    public static async Task ResetAsync(string connectionString)
    {
        await using (var db = new BulkContext(CreateOptions(connectionString)))
        {
            await db.Database.EnsureCreatedAsync();
        }

        await TruncateAsync(connectionString);
    }

    public static async Task TruncateAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("TRUNCATE TABLE dbo.OrderLines;", connection);
        await command.ExecuteNonQueryAsync();
    }

    public static async Task<int> CountAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.OrderLines;", connection);
        return (int)(await command.ExecuteScalarAsync())!;
    }
}

/// <summary>Counts every command EF executes and keeps the text and parameter count of the first one.</summary>
public sealed class CommandCounter : DbCommandInterceptor
{
    public int Commands { get; private set; }
    public string? FirstCommandText { get; private set; }
    public int FirstCommandParameterCount { get; private set; }
    public int MaxParameterCount { get; private set; }

    public void Reset()
    {
        Commands = 0;
        FirstCommandText = null;
        FirstCommandParameterCount = 0;
        MaxParameterCount = 0;
    }

    private void Record(System.Data.Common.DbCommand command)
    {
        Commands++;
        MaxParameterCount = Math.Max(MaxParameterCount, command.Parameters.Count);
        if (FirstCommandText != null) return;
        FirstCommandText = command.CommandText;
        FirstCommandParameterCount = command.Parameters.Count;
    }

    public override InterceptionResult<System.Data.Common.DbDataReader> ReaderExecuting(
        System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
        System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<System.Data.Common.DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Record(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        System.Data.Common.DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return ValueTask.FromResult(result);
    }
}
