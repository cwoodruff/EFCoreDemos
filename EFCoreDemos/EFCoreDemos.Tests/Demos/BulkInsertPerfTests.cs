extern alias bulk_insert_perf;

using EFCoreDemos.Tests.Infrastructure;
using bulk_insert_perf::bulk_insert_perf;
using bulk_insert_perf::bulk_insert_perf.Data;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// bulk-insert-perf: the same rows inserted per row, with AddRange (EF's default batching), with a larger
    /// MaxBatchSize, with AutoDetectChanges off and with SqlBulkCopy. Every approach must insert exactly N rows;
    /// batching must send fewer commands than rows. Works in its own uniquely-named database, dropped on dispose.
    /// </summary>
    public class BulkInsertPerfTests : IDisposable
    {
        // EF Core's default MaxBatchSize for SQL Server.
        private const int DefaultBatchSize = 42;

        private readonly string _connectionString =
            SqlServerTestServer.ConnectionString + $"Database=BulkInsertTest_{Guid.NewGuid():N};";

        private bool _created;

        private async Task ResetAsync()
        {
            _created = true;
            await BulkContext.ResetAsync(_connectionString);
        }

        public void Dispose()
        {
            if (!_created) return;
            using var db = new BulkContext(BulkContext.CreateOptions(_connectionString));
            db.Database.EnsureDeleted();
        }

        [Fact]
        public void Generate_IsDeterministic()
        {
            var first = OrderLine.Generate(100);
            var second = OrderLine.Generate(100);

            Assert.Equal(100, first.Count);
            Assert.Equal(
                first.Select(r => (r.OrderNumber, r.Sku, r.Quantity, r.UnitPrice, r.OrderedAt)),
                second.Select(r => (r.OrderNumber, r.Sku, r.Quantity, r.UnitPrice, r.OrderedAt)));
        }

        [SqlServerFact]
        public async Task PerRowSaveChanges_SendsOneCommandPerRow()
        {
            await ResetAsync();
            var counter = new CommandCounter();

            await BulkInserts.PerRowSaveChangesAsync(
                BulkContext.CreateOptions(_connectionString, counter: counter), OrderLine.Generate(50));

            Assert.Equal(50, await BulkContext.CountAsync(_connectionString));
            Assert.Equal(50, counter.Commands);
        }

        [SqlServerFact]
        public async Task AddRange_BatchesRowsIntoMergeStatements_AndReadsBackTheIds()
        {
            await ResetAsync();
            var counter = new CommandCounter();
            var rows = OrderLine.Generate(500);

            await BulkInserts.AddRangeAsync(BulkContext.CreateOptions(_connectionString, counter: counter), rows);

            Assert.Equal(500, await BulkContext.CountAsync(_connectionString));
            Assert.Equal((int)Math.Ceiling(500.0 / DefaultBatchSize), counter.Commands);
            Assert.Equal(DefaultBatchSize * 5, counter.FirstCommandParameterCount);
            Assert.Contains("MERGE [OrderLines]", counter.FirstCommandText);
            Assert.Contains("OUTPUT INSERTED.[Id]", counter.FirstCommandText);

            // The generated IDENTITY values flowed back into the entities.
            Assert.Equal(Enumerable.Range(1, 500), rows.Select(r => r.Id).Order());
        }

        [SqlServerFact]
        public async Task TunedMaxBatchSize_SendsFewerLargerCommands_UnderTheParameterLimit()
        {
            await ResetAsync();
            var counter = new CommandCounter();

            await BulkInserts.AddRangeAsync(
                BulkContext.CreateOptions(_connectionString, BulkInserts.TunedBatchSize, counter), OrderLine.Generate(1_000));

            Assert.Equal(1_000, await BulkContext.CountAsync(_connectionString));
            Assert.Equal(3, counter.Commands); // 400 + 400 + 200
            Assert.Equal(BulkInserts.TunedBatchSize * 5, counter.MaxParameterCount);
            Assert.True(counter.MaxParameterCount <= 2_100, "SQL Server rejects commands with more than 2,100 parameters.");
        }

        [SqlServerFact]
        public async Task AddWithoutDetectChanges_StillInsertsEveryRow()
        {
            await ResetAsync();
            var counter = new CommandCounter();

            await BulkInserts.AddWithoutDetectChangesAsync(
                BulkContext.CreateOptions(_connectionString, counter: counter), OrderLine.Generate(500));

            Assert.Equal(500, await BulkContext.CountAsync(_connectionString));
            Assert.Equal((int)Math.Ceiling(500.0 / DefaultBatchSize), counter.Commands);
        }

        [SqlServerFact]
        public async Task SqlBulkCopy_InsertsEveryRowWithTheSameValues()
        {
            await ResetAsync();
            var rows = OrderLine.Generate(500);

            await BulkInserts.SqlBulkCopyAsync(_connectionString, rows);

            Assert.Equal(500, await BulkContext.CountAsync(_connectionString));

            await using var db = new BulkContext(BulkContext.CreateOptions(_connectionString));
            var saved = db.OrderLines.OrderBy(r => r.Id).ToList();
            Assert.Equal(
                rows.Select(r => (r.OrderNumber, r.Sku, r.Quantity, r.UnitPrice, r.OrderedAt)),
                saved.Select(r => (r.OrderNumber, r.Sku, r.Quantity, r.UnitPrice, r.OrderedAt)));
        }

        [SqlServerFact]
        public async Task Reset_EmptiesTheTableAndRestartsTheIdentity()
        {
            await ResetAsync();
            await BulkInserts.AddRangeAsync(BulkContext.CreateOptions(_connectionString), OrderLine.Generate(10));

            await BulkContext.ResetAsync(_connectionString);
            var rows = OrderLine.Generate(1);
            await BulkInserts.AddRangeAsync(BulkContext.CreateOptions(_connectionString), rows);

            Assert.Equal(1, await BulkContext.CountAsync(_connectionString));
            Assert.Equal(1, rows[0].Id);
        }
    }
}
