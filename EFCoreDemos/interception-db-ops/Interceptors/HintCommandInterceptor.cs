using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace interception_db_ops.Interceptors;

public class HintCommandInterceptor : DbCommandInterceptor
{
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        ManipulateCommand(command, eventData);
        return result;
    }

    // Async queries (ToListAsync, AsAsyncEnumerable, ...) go through this path, not ReaderExecuting.
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        ManipulateCommand(command, eventData);
        return new ValueTask<InterceptionResult<DbDataReader>>(result);
    }

    private static void ManipulateCommand(DbCommand command, CommandEventData eventData)
    {
        // Manipulate the command text, etc. here...
        if (eventData.Context?.Database.IsSqlServer() == true)
        {
            // OPTION (...) is a SQL Server query hint; SQLite would reject it with a syntax error.
            command.CommandText += " OPTION (OPTIMIZE FOR UNKNOWN)";
        }
        else
        {
            command.CommandText = "-- Rewritten by HintCommandInterceptor" + Environment.NewLine + command.CommandText;
        }
    }
}
