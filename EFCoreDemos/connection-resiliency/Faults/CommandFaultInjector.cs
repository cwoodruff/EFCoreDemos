using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace connection_resiliency.Faults;

/// <summary>
/// Simulates a flaky network or a failover: while armed, the first N commands whose SQL contains a given text
/// throw a transient <see cref="Microsoft.Data.SqlClient.SqlException"/> before they reach the server.
/// Every command seen while armed is reported through <see cref="Log"/>.
/// </summary>
public sealed partial class CommandFaultInjector : DbCommandInterceptor
{
    private readonly Lock _lock = new();
    private bool _armed;
    private string _target = "";
    private int _failuresLeft;
    private int _errorNumber;

    public Action<string>? Log { get; set; }

    /// <summary>Commands containing the target text since the last <see cref="Arm"/> (failed ones included).</summary>
    public int Attempts { get; private set; }

    /// <summary>Exceptions thrown since the last <see cref="Arm"/>.</summary>
    public int Injected { get; private set; }

    /// <param name="failures">How many matching commands to fail before letting them through.</param>
    /// <param name="commandContains">SQL text that identifies the target command, e.g. "INSERT INTO [Orders]".</param>
    /// <param name="errorNumber">The SqlException error number to throw.</param>
    public void Arm(int failures, string commandContains, int errorNumber = TransientSqlException.ConnectionReset)
    {
        lock (_lock)
        {
            _armed = true;
            _target = commandContains;
            _failuresLeft = failures;
            _errorNumber = errorNumber;
            Attempts = 0;
            Injected = 0;
        }
    }

    public void Disarm()
    {
        lock (_lock) _armed = false;
    }

    private void Intercept(DbCommand command)
    {
        lock (_lock)
        {
            if (!_armed) return;

            var description = Describe(command.CommandText);
            if (!command.CommandText.Contains(_target, StringComparison.Ordinal))
            {
                Log?.Invoke($"    {description,-33} -> ok");
                return;
            }

            Attempts++;
            if (_failuresLeft <= 0)
            {
                Log?.Invoke($"    {description,-33} -> ok");
                return;
            }

            _failuresLeft--;
            Injected++;
            Log?.Invoke($"    {description,-33} -> SqlException {_errorNumber} (injected)");
            throw TransientSqlException.Create(_errorNumber, $"Injected fault {_errorNumber} (connection-resiliency demo).");
        }
    }

    /// <summary>
    /// "INSERT INTO [Orders]" / "SELECT ... FROM [Orders]" - enough to follow the flow without the full SQL.
    /// A batched SaveChanges lists every table it inserts into: "INSERT INTO [Orders] + [Payments]".
    /// </summary>
    private static string Describe(string sql)
    {
        var inserts = InsertInto().Matches(sql).Select(m => $"[{m.Groups[1].Value}]").Distinct().ToList();
        if (inserts.Count > 0) return "INSERT INTO " + string.Join(" + ", inserts);
        var from = SelectFrom().Match(sql);
        return from.Success ? $"SELECT ... FROM [{from.Groups[1].Value}]" : sql.Split('\n')[0].Trim();
    }

    [GeneratedRegex(@"INSERT INTO \[(\w+)\]")]
    private static partial Regex InsertInto();

    [GeneratedRegex(@"FROM \[(\w+)\]")]
    private static partial Regex SelectFrom();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Intercept(command);
        return result;
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Intercept(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        Intercept(command);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Intercept(command);
        return ValueTask.FromResult(result);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Intercept(command);
        return result;
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Intercept(command);
        return ValueTask.FromResult(result);
    }
}
