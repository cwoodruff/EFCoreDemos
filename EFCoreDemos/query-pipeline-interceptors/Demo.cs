using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using query_pipeline_interceptors.Chinook;

namespace query_pipeline_interceptors;

public static class Demo
{
    /// <summary>The copy of chinook.db next to the binary, read-only.</summary>
    public static string ConnectionString =>
        $"Data Source={Path.Combine(AppContext.BaseDirectory, "chinook.db")};Mode=ReadOnly";

    /// <summary>Context options with the given interceptors and, optionally, a sink for executed SQL.</summary>
    public static DbContextOptions<ChinookContext> Options(string connectionString, IEnumerable<IInterceptor> interceptors,
        Action<string>? sqlLog = null)
    {
        var builder = new DbContextOptionsBuilder<ChinookContext>()
            .UseSqlite(connectionString)
            .AddInterceptors(interceptors);

        if (sqlLog != null)
        {
            builder.LogTo(sqlLog, [DbLoggerCategory.Database.Command.Name], LogLevel.Information);
        }

        return builder.Options;
    }

    /// <summary>The SQL text of an "Executed DbCommand" log entry (drops the header line).</summary>
    public static string SqlOf(string logEntry) =>
        string.Join(Environment.NewLine, logEntry.Split('\n').Skip(2).Select(l => l.Trim()));
}
