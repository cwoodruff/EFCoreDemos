using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace connection_resiliency.Faults;

/// <summary>
/// Simulates the worst transient failure: the COMMIT reaches the server and succeeds, but the connection drops
/// before the acknowledgement gets back. The interceptor runs after the real commit and then throws a transient
/// <see cref="Microsoft.Data.SqlClient.SqlException"/>, so the caller sees a failure for work that was saved.
/// </summary>
public sealed class CommitAckFaultInjector : DbTransactionInterceptor
{
    private readonly Lock _lock = new();
    private int _failuresLeft;

    public Action<string>? Log { get; set; }

    /// <summary>Commits seen since the last <see cref="Arm"/>.</summary>
    public int Commits { get; private set; }

    public void Arm(int failures)
    {
        lock (_lock)
        {
            _failuresLeft = failures;
            Commits = 0;
        }
    }

    public void Disarm() => Arm(0);

    private void AfterCommit()
    {
        lock (_lock)
        {
            Commits++;
            if (_failuresLeft <= 0)
            {
                Log?.Invoke($"    {"COMMIT",-33} -> ok");
                return;
            }

            _failuresLeft--;
            Log?.Invoke($"    {"COMMIT",-33} -> committed on the server, then SqlException " +
                        $"{TransientSqlException.ConnectionReset} on the way back (injected)");
        }

        throw TransientSqlException.Create(TransientSqlException.ConnectionReset,
            "Injected fault after a successful commit (connection-resiliency demo).");
    }

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        AfterCommit();

    public override Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        AfterCommit();
        return Task.CompletedTask;
    }
}
