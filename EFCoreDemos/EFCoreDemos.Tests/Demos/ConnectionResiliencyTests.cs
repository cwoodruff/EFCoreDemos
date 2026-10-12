extern alias connection_resiliency;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using connection_resiliency::connection_resiliency;
using connection_resiliency::connection_resiliency.Data;
using connection_resiliency::connection_resiliency.Faults;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>One uniquely-named SQL Server database for the whole test class, dropped when the class is done.</summary>
    public sealed class ResiliencyDatabase : IDisposable
    {
        public string ConnectionString { get; } =
            SqlServerTestServer.ConnectionString + $"Database=ResiliencyTest_{Guid.NewGuid():N};";

        public bool Created { get; set; }

        public void Dispose()
        {
            if (Created) ResiliencyContext.DropAsync(ConnectionString).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// connection-resiliency: EnableRetryOnFailure retries transient SqlExceptions (simulated by an interceptor),
    /// refuses user-started transactions, replays a whole unit of work under CreateExecutionStrategy().ExecuteAsync,
    /// and ExecuteInTransactionAsync + verifySucceeded keeps a lost commit acknowledgement from saving twice.
    /// </summary>
    public class ConnectionResiliencyTests(ResiliencyDatabase database) : IClassFixture<ResiliencyDatabase>
    {
        private readonly FaultInjection _faults = new();

        private async Task ResetAsync()
        {
            database.Created = true;
            await ResiliencyContext.ResetAsync(database.ConnectionString);
        }

        // Fresh interceptor instances per test: keep them out of EF's process-wide service provider cache.
        private DbContextOptions<ResiliencyContext> Options(bool retryOnFailure = true, int[]? errorNumbersToAdd = null) =>
            ResiliencyContext.CreateOptions(database.ConnectionString, _faults, retryOnFailure,
                errorNumbersToAdd: errorNumbersToAdd, cacheServiceProvider: false);

        private Task<(int Orders, int Payments)> CountAsync(Order order) =>
            ResiliencyContext.CountAsync(database.ConnectionString, order.Reference);

        private static SqlException? Underlying(Exception? e)
        {
            for (; e != null; e = e.InnerException)
            {
                if (e is SqlException sql) return sql;
            }

            return null;
        }

        [Fact]
        public void TransientSqlException_CarriesTheRequestedErrorNumber()
        {
            var ex = TransientSqlException.Create(TransientSqlException.DatabaseUnavailable, "boom");

            Assert.Equal(40613, ex.Number);
            Assert.Equal("boom", ex.Message);
        }

        [SqlServerFact]
        public async Task WithoutRetries_ATransientErrorSurfaces()
        {
            await ResetAsync();
            var order = Scenarios.NewOrder("Ada", 10m);
            _faults.Commands.Arm(1, "INSERT INTO [Orders]");

            var outcome = await Scenarios.SaveOrderAsync(Options(retryOnFailure: false), order);

            Assert.False(outcome.Succeeded);
            Assert.Equal(1, _faults.Commands.Attempts);
            Assert.Contains("EnableRetryOnFailure", outcome.Error!.Message);
            Assert.Equal(TransientSqlException.ConnectionReset, Underlying(outcome.Error)?.Number);
            Assert.Equal((0, 0), await CountAsync(order));
        }

        [SqlServerFact]
        public async Task EnableRetryOnFailure_RetriesTransientErrors_UntilTheSaveSucceeds()
        {
            await ResetAsync();
            var order = Scenarios.NewOrder("Grace", 20m);
            _faults.Commands.Arm(2, "INSERT INTO [Orders]");

            var outcome = await Scenarios.SaveOrderAsync(Options(), order);

            Assert.True(outcome.Succeeded, outcome.Error?.ToString());
            Assert.Equal(3, _faults.Commands.Attempts);
            Assert.Equal(2, _faults.Commands.Injected);
            Assert.Equal((1, 0), await CountAsync(order));
        }

        [SqlServerFact]
        public async Task EnableRetryOnFailure_RetriesQueriesToo()
        {
            await ResetAsync();
            _faults.Commands.Arm(1, "FROM [Orders]");

            var (outcome, count) = await Scenarios.CountOrdersAsync(Options());

            Assert.True(outcome.Succeeded, outcome.Error?.ToString());
            Assert.Equal(0, count);
            Assert.Equal(2, _faults.Commands.Attempts);
        }

        [SqlServerFact]
        public async Task MoreFailuresThanMaxRetryCount_ThrowsRetryLimitExceeded()
        {
            await ResetAsync();
            var order = Scenarios.NewOrder("Alan", 30m);
            _faults.Commands.Arm(5, "INSERT INTO [Orders]");

            var outcome = await Scenarios.SaveOrderAsync(Options(), order);

            Assert.IsType<RetryLimitExceededException>(outcome.Error);
            Assert.Equal(4, _faults.Commands.Attempts); // the first try + maxRetryCount (3) retries
            Assert.Equal(TransientSqlException.ConnectionReset, Underlying(outcome.Error)?.Number);
            Assert.Equal((0, 0), await CountAsync(order));
        }

        [SqlServerFact]
        public async Task FailoverErrors_WaitAtLeastFiveSeconds_DespiteMaxRetryDelay()
        {
            // The demo's options cap maxRetryDelay at 10 ms; EF Core 10 still holds 40613 back for ~5 s.
            await ResetAsync();
            _faults.Commands.Arm(1, "INSERT INTO [Orders]", errorNumber: TransientSqlException.DatabaseUnavailable);
            var clock = System.Diagnostics.Stopwatch.StartNew();

            var outcome = await Scenarios.SaveOrderAsync(Options(), Scenarios.NewOrder("Tim", 55m));

            Assert.True(outcome.Succeeded, outcome.Error?.ToString());
            Assert.Equal(2, _faults.Commands.Attempts);
            Assert.True(clock.Elapsed >= TimeSpan.FromSeconds(4.5), $"Retry came after only {clock.Elapsed}.");
        }

        [SqlServerFact]
        public async Task NonTransientErrors_AreNotRetried()
        {
            await ResetAsync();
            _faults.Commands.Arm(1, "INSERT INTO [Orders]", errorNumber: 547);

            var outcome = await Scenarios.SaveOrderAsync(Options(), Scenarios.NewOrder("Edsger", 40m));

            Assert.IsType<DbUpdateException>(outcome.Error);
            Assert.Equal(547, Underlying(outcome.Error)?.Number);
            Assert.Equal(1, _faults.Commands.Attempts);
        }

        [SqlServerFact]
        public async Task ErrorNumbersToAdd_MakesACustomErrorRetriable()
        {
            await ResetAsync();
            var order = Scenarios.NewOrder("Barbara", 50m);
            _faults.Commands.Arm(1, "INSERT INTO [Orders]", errorNumber: 50000);

            var outcome = await Scenarios.SaveOrderAsync(Options(errorNumbersToAdd: [50000]), order);

            Assert.True(outcome.Succeeded, outcome.Error?.ToString());
            Assert.Equal(2, _faults.Commands.Attempts);
            Assert.Equal((1, 0), await CountAsync(order));
        }

        [SqlServerFact]
        public async Task UserStartedTransaction_WithRetryingStrategy_IsRefused()
        {
            await ResetAsync();
            var order = Scenarios.NewOrder("Linus", 60m);

            var outcome = await Scenarios.PlaceOrderInUserTransactionAsync(Options(), order, Scenarios.PaymentFor(order));

            var error = Assert.IsType<InvalidOperationException>(outcome.Error);
            Assert.Contains("does not support user-initiated transactions", error.Message);
            Assert.Contains("CreateExecutionStrategy", error.Message);
            Assert.Equal("SaveChangesAsync (order)", outcome.ThrownBy);
            Assert.Equal((0, 0), await CountAsync(order));
        }

        [SqlServerFact]
        public async Task UserStartedTransaction_WithoutRetries_IsAllowed()
        {
            await ResetAsync();
            var order = Scenarios.NewOrder("Ken", 65m);

            var outcome = await Scenarios.PlaceOrderInUserTransactionAsync(
                Options(retryOnFailure: false), order, Scenarios.PaymentFor(order));

            Assert.True(outcome.Succeeded, outcome.Error?.ToString());
            Assert.Equal((1, 1), await CountAsync(order));
        }

        [SqlServerFact]
        public async Task ExecutionStrategy_ReplaysTheWholeUnitOfWork()
        {
            await ResetAsync();
            var order = Scenarios.NewOrder("Margaret", 70m);
            _faults.Commands.Arm(1, "INSERT INTO [Payments]");

            var (outcome, attempts) =
                await Scenarios.PlaceOrderWithExecutionStrategyAsync(Options(), order, Scenarios.PaymentFor(order));

            Assert.True(outcome.Succeeded, outcome.Error?.ToString());
            Assert.Equal(2, attempts);
            // The first attempt's order INSERT was rolled back with its transaction: exactly one of each.
            Assert.Equal((1, 1), await CountAsync(order));
        }

        [SqlServerFact]
        public async Task LostCommitAcknowledgement_WithoutVerification_SavesTwice()
        {
            await ResetAsync();
            var order = Scenarios.NewOrder("Katherine", 80m);
            _faults.Commits.Arm(1);

            var (outcome, attempts) =
                await Scenarios.PlaceOrderWithExecutionStrategyAsync(Options(), order, Scenarios.PaymentFor(order));

            Assert.True(outcome.Succeeded, outcome.Error?.ToString());
            Assert.Equal(2, attempts);
            Assert.Equal(2, _faults.Commits.Commits);
            Assert.Equal((2, 2), await CountAsync(order));
        }

        [SqlServerFact]
        public async Task LostCommitAcknowledgement_WithVerifySucceeded_SavesExactlyOnce()
        {
            await ResetAsync();
            var order = Scenarios.NewOrder("Frances", 90m);
            _faults.Commits.Arm(1);

            var (outcome, saves, verifications) =
                await Scenarios.PlaceOrderVerifiedAsync(Options(), order, Scenarios.PaymentFor(order));

            Assert.True(outcome.Succeeded, outcome.Error?.ToString());
            Assert.Equal(1, saves);
            Assert.Equal(1, verifications);
            Assert.Equal(1, _faults.Commits.Commits);
            Assert.Equal((1, 1), await CountAsync(order));
        }

        [SqlServerFact]
        public async Task VerifySucceeded_IsNotCalled_WhenTheCommitIsAcknowledged()
        {
            await ResetAsync();
            var order = Scenarios.NewOrder("Niklaus", 95m);

            var (outcome, saves, verifications) =
                await Scenarios.PlaceOrderVerifiedAsync(Options(), order, Scenarios.PaymentFor(order));

            Assert.True(outcome.Succeeded, outcome.Error?.ToString());
            Assert.Equal(1, saves);
            Assert.Equal(0, verifications);
            Assert.Equal((1, 1), await CountAsync(order));
        }
    }
}
