using connection_resiliency;
using connection_resiliency.Data;
using connection_resiliency.Faults;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

const string connectionString =
    "Server=localhost,1433;User Id=sa;Password=8riwudeg!!;Database=ResiliencyDemo;TrustServerCertificate=True;" +
    "Application Name=EFCoreDemos-ConnectionResiliency;";

await ResiliencyContext.ResetAsync(connectionString);

// One set of fault injectors for every context: same interceptor instances -> one cached EF service provider.
var faults = new FaultInjection { Log = Console.WriteLine };
var noRetry = ResiliencyContext.CreateOptions(connectionString, faults, retryOnFailure: false);
var retry = ResiliencyContext.CreateOptions(connectionString, faults);
var retryWithCustomError = ResiliencyContext.CreateOptions(connectionString, faults, errorNumbersToAdd: [50000]);

Console.WriteLine("Database: ResiliencyDemo (created if missing, emptied every run)");
Console.WriteLine($"Faults: a DbCommandInterceptor throws a real SqlException {TransientSqlException.ConnectionReset} " +
                  "(\"connection forcibly closed by the remote host\")");
Console.WriteLine("before chosen commands reach the server.");

Section("1. EnableRetryOnFailure: what gets retried");
Console.WriteLine("""
    UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
        maxRetryCount: 3, maxRetryDelay: TimeSpan.FromMilliseconds(10), errorNumbersToAdd: []))
    (the 10 ms cap only keeps the demo fast - the defaults are 6 retries, backing off exponentially up to 30 s)

    """);

Sub("a) No retry strategy, one transient error");
faults.Commands.Arm(1, "INSERT INTO [Orders]");
Report(await Scenarios.SaveOrderAsync(noRetry, Scenarios.NewOrder("Ada", 10m)), faults.Commands.Attempts);

Sub("b) EnableRetryOnFailure, the INSERT fails twice");
faults.Commands.Arm(2, "INSERT INTO [Orders]");
Report(await Scenarios.SaveOrderAsync(retry, Scenarios.NewOrder("Grace", 20m)), faults.Commands.Attempts);

Sub("c) Queries are retried the same way");
faults.Commands.Arm(1, "FROM [Orders]");
var (countOutcome, count) = await Scenarios.CountOrdersAsync(retry);
Report(countOutcome, faults.Commands.Attempts, $"{count} order(s) counted");

Sub("d) More failures than maxRetryCount (5 failures, 3 retries)");
faults.Commands.Arm(5, "INSERT INTO [Orders]");
Report(await Scenarios.SaveOrderAsync(retry, Scenarios.NewOrder("Alan", 30m)), faults.Commands.Attempts);

Sub("e) A non-transient error (547, constraint violation) is never retried");
faults.Commands.Arm(1, "INSERT INTO [Orders]", errorNumber: 547);
Report(await Scenarios.SaveOrderAsync(retry, Scenarios.NewOrder("Edsger", 40m)), faults.Commands.Attempts);

Sub("f) errorNumbersToAdd: [50000] makes your own error number (e.g. from RAISERROR/THROW) transient");
faults.Commands.Arm(1, "INSERT INTO [Orders]", errorNumber: 50000);
Report(await Scenarios.SaveOrderAsync(retryWithCustomError, Scenarios.NewOrder("Barbara", 50m)), faults.Commands.Attempts);

Sub($"g) Not every transient error waits maxRetryDelay: Azure SQL failover ({TransientSqlException.DatabaseUnavailable}), watch the pause");
faults.Commands.Arm(1, "INSERT INTO [Orders]", errorNumber: TransientSqlException.DatabaseUnavailable);
var clock = Stopwatch.StartNew();
var failover = await Scenarios.SaveOrderAsync(retry, Scenarios.NewOrder("Tim", 55m));
Report(failover, faults.Commands.Attempts, $"retry came after {clock.Elapsed.TotalSeconds:0.0} s despite maxRetryDelay = 10 ms");
Console.WriteLine("""
   EF Core 10 waits at least 5 s before retrying some errors - of those tried for this demo: 40613 (failover),
   40501 (service busy), 49920 (too many operations) and 233 - so a failover has time to finish. Others,
   10054 and 1205 included, use the normal back-off.
""");

Console.WriteLine("""

    EF retries an operation it runs itself - one SaveChanges, one query - when SqlServerTransientExceptionDetector
    recognises the error number (dropped connection 10054, deadlock 1205, failover 40613, throttling 10928/49920, ...)
    or you added it. Timeouts (-2) and logic errors (547, 2627, ...) surface at once.
    """);

Section("2. The bug: a transaction YOU start, with retries switched on");
Console.WriteLine("""
    await using var transaction = await db.Database.BeginTransactionAsync();
    db.Orders.Add(order);    await db.SaveChangesAsync();
    db.Payments.Add(payment); await db.SaveChangesAsync();
    await transaction.CommitAsync();

    """);
faults.Disarm();
var bugOrder = Scenarios.NewOrder("Linus", 60m);
var bug = await Scenarios.PlaceOrderInUserTransactionAsync(retry, bugOrder, Scenarios.PaymentFor(bugOrder));
Console.WriteLine($"No fault injected at all - and still: {bug.ThrownBy} threw");
Console.WriteLine($"  {bug.Error?.GetType().Name}: {bug.Error?.Message}");
Console.WriteLine($"Rows saved for this order: {Rows(await ResiliencyContext.CountAsync(connectionString, bugOrder.Reference))}");
Console.WriteLine("""

    EF refuses up front. If the payment INSERT failed and EF retried only that statement, the connection - and with
    it the order INSERT - would already have been rolled back, so a "successful" retry would commit half a unit
    of work. Only you know where the unit of work starts, so you have to hand all of it to the strategy.
    """);

Section("3. The fix: wrap the whole unit of work in the execution strategy");
Console.WriteLine("""
    var strategy = db.Database.CreateExecutionStrategy();
    await strategy.ExecuteAsync(async () =>
    {
        await using var context = new ResiliencyContext(options);   // fresh per attempt
        await using var transaction = await context.Database.BeginTransactionAsync();
        context.Orders.Add(...);   await context.SaveChangesAsync();
        context.Payments.Add(...); await context.SaveChangesAsync();
        await transaction.CommitAsync();
    });

    The payment INSERT fails once:
    """);
faults.Commands.Arm(1, "INSERT INTO [Payments]");
var fixOrder = Scenarios.NewOrder("Margaret", 70m);
var (fix, fixAttempts) = await Scenarios.PlaceOrderWithExecutionStrategyAsync(retry, fixOrder, Scenarios.PaymentFor(fixOrder));
faults.Disarm();
Console.WriteLine(fix.Succeeded ? $"Succeeded after {fixAttempts} attempts of the whole unit of work." : $"FAILED: {fix.Error?.Message}");
Console.WriteLine($"Rows saved for this order: {Rows(await ResiliencyContext.CountAsync(connectionString, fixOrder.Reference))}");
Console.WriteLine("Attempt 1's order INSERT was rolled back with its transaction; attempt 2 replayed everything.");

Section("4. The catch: the commit succeeded, but the acknowledgement was lost");
Console.WriteLine("""
    A connection can drop after SQL Server commits but before the client hears "OK". To the strategy that looks
    like any transient failure, so it runs the unit of work again.

    a) The section 3 code, COMMIT acknowledgement lost once:
    """);
faults.Commands.Arm(0, "INSERT INTO"); // log the INSERTs, fail none
faults.Commits.Arm(1);
var lostOrder = Scenarios.NewOrder("Katherine", 80m);
var (lost, lostAttempts) = await Scenarios.PlaceOrderWithExecutionStrategyAsync(retry, lostOrder, Scenarios.PaymentFor(lostOrder));
faults.Disarm();
Console.WriteLine(lost.Succeeded ? $"Reported success after {lostAttempts} attempts." : $"FAILED: {lost.Error?.Message}");
Console.WriteLine($"Rows saved for this order: {Rows(await ResiliencyContext.CountAsync(connectionString, lostOrder.Reference))}  <- charged twice");

Console.WriteLine("""

    b) ExecuteInTransactionAsync with a verifySucceeded callback:

    db.Orders.Add(order); db.Payments.Add(payment);
    await strategy.ExecuteInTransactionAsync(db,
        operation: (ctx, ct) => ctx.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct),
        verifySucceeded: (ctx, ct) => ctx.Orders.AsNoTracking().AnyAsync(o => o.Reference == order.Reference, ct));
    db.ChangeTracker.AcceptAllChanges();

    Same lost acknowledgement:
    """);
faults.Commands.Arm(0, "INSERT INTO");
faults.Commits.Arm(1);
var safeOrder = Scenarios.NewOrder("Frances", 90m);
var (safe, saves, verifications) = await Scenarios.PlaceOrderVerifiedAsync(retry, safeOrder, Scenarios.PaymentFor(safeOrder));
faults.Disarm();
Console.WriteLine(safe.Succeeded
    ? $"Succeeded: SaveChanges ran {saves} time(s), verifySucceeded was asked {verifications} time(s) and found the order."
    : $"FAILED: {safe.Error?.Message}");
Console.WriteLine($"Rows saved for this order: {Rows(await ResiliencyContext.CountAsync(connectionString, safeOrder.Reference))}  <- exactly once");
Console.WriteLine("""

    verifySucceeded only runs when the COMMIT itself threw. It needs something the client knows before saving -
    here a client-generated Reference - to look the work up. acceptAllChangesOnSuccess: false keeps the entities
    Added until the outcome is known, so if the check says "not saved" the retry can save them again.

    Takeaways
      - EnableRetryOnFailure covers single operations EF runs for you. Nothing to change for plain SaveChanges.
      - Your own BeginTransaction + retries = InvalidOperationException. Wrap the whole unit in
        CreateExecutionStrategy().ExecuteAsync(...), using a fresh context per attempt.
      - Retries can repeat work that already committed. Make it idempotent (a client key + verifySucceeded,
        or a unique constraint) wherever doing it twice matters.
    """);

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 100));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 100));
}

static void Sub(string title)
{
    Console.WriteLine();
    Console.WriteLine($"-- {title} --");
}

static void Report(Outcome outcome, int attempts, string? detail = null)
{
    if (outcome.Succeeded)
    {
        Console.WriteLine($"=> succeeded after {attempts} attempt(s){(detail is null ? "" : $": {detail}")}");
        return;
    }

    Console.WriteLine($"=> {outcome.ThrownBy} threw after {attempts} attempt(s): {outcome.Error!.GetType().Name}");
    Console.WriteLine($"   {outcome.Error.Message}");
    for (var inner = outcome.Error.InnerException; inner != null; inner = inner.InnerException)
    {
        if (inner is SqlException sql)
        {
            Console.WriteLine($"   underlying: SqlException {sql.Number}");
            break;
        }
    }
}

static string Rows((int Orders, int Payments) rows) => $"{rows.Orders} order(s), {rows.Payments} payment(s)";
