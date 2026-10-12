using System.Reflection;
using Microsoft.Data.SqlClient;

namespace connection_resiliency.Faults;

/// <summary>
/// Builds a real <see cref="SqlException"/> carrying a chosen error number. SqlClient has no public constructor
/// for it, so this goes through the internal factory SqlClient itself uses when the server reports an error.
/// Demo and test code only.
/// </summary>
public static class TransientSqlException
{
    /// <summary>
    /// 10054 "An existing connection was forcibly closed by the remote host": a dropped connection, which EF Core's
    /// SqlServerTransientExceptionDetector treats as transient. The demo's default injected fault.
    /// </summary>
    public const int ConnectionReset = 10054;

    /// <summary>
    /// 40613 "Database is not currently available": the Azure SQL failover error. Also transient, but EF Core 10
    /// waits at least 5 seconds before retrying it, whatever maxRetryDelay says.
    /// </summary>
    public const int DatabaseUnavailable = 40613;

    public static SqlException Create(int number, string message)
    {
        var collection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;
        typeof(SqlErrorCollection)
            .GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(collection, [CreateError(number, message)]);

        var create = typeof(SqlException).GetMethod("CreateException", BindingFlags.Static | BindingFlags.NonPublic,
            [typeof(SqlErrorCollection), typeof(string)])!;
        return (SqlException)create.Invoke(null, [collection, "17.0.0"])!;
    }

    private static SqlError CreateError(int number, string message)
    {
        // The internal constructor's shape varies between SqlClient versions: fill it by parameter name and type.
        var ctor = typeof(SqlError).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var args = ctor.GetParameters().Select(p => p.Name switch
        {
            "infoNumber" => number,
            "errorMessage" => message,
            "errorClass" => (byte)20,
            "server" => "localhost",
            _ when p.ParameterType == typeof(string) => "",
            _ when p.ParameterType == typeof(byte) => (byte)0,
            _ when p.ParameterType == typeof(int) => 0,
            _ when p.ParameterType == typeof(uint) => 0u,
            _ => (object?)null
        }).ToArray();

        return (SqlError)ctor.Invoke(args);
    }
}
