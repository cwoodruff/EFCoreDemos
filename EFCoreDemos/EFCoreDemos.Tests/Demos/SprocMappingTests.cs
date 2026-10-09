extern alias sproc_mapping;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using sproc_mapping::sproc_mapping.Chinook;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// sproc-mapping: Insert/Update/DeleteUsingStoredProcedure make SaveChanges call the Chinook
    /// sproc_Insert*/sproc_Update*/sproc_Delete* procedures (SQL Server only) instead of emitting DML.
    /// All writes run inside a transaction that is rolled back.
    /// </summary>
    public class SprocMappingTests
    {
        private static ChinookContext CreateSqlServerContext(SqlLog? log = null)
        {
            var builder = new DbContextOptionsBuilder<ChinookContext>()
                .UseSqlServer(SqlServerTestServer.ConnectionString + "Database=Chinook;");
            if (log != null)
            {
                builder.LogTo(log.Add, [DbLoggerCategory.Database.Command.Name], LogLevel.Information);
            }

            return new ChinookContext(builder.Options);
        }

        private static Track NewTrack() => new()
        {
            Name = "Sproc Mapping Test",
            AlbumId = 1,
            MediaTypeId = 1,
            GenreId = 1,
            Composer = "EF Core",
            Milliseconds = 180000,
            Bytes = 1024,
            UnitPrice = 0.99m
        };

        [Fact]
        public void Model_MapsTrackCudToStoredProcedures_OnSqlServer()
        {
            using var db = new ChinookContext();
            var track = db.Model.FindEntityType(typeof(Track))!;

            Assert.Equal("sproc_InsertTrack", track.GetInsertStoredProcedure()?.Name);
            Assert.Equal("sproc_UpdateTrack", track.GetUpdateStoredProcedure()?.Name);
            Assert.Equal("sproc_DeleteTrack", track.GetDeleteStoredProcedure()?.Name);

            // The generated Id comes back through an OUTPUT parameter.
            var insert = track.GetInsertStoredProcedure()!;
            var idParameter = insert.Parameters.Single(p => p.PropertyName == nameof(Track.Id));
            Assert.Equal(System.Data.ParameterDirection.Output, idParameter.Direction);
        }

        [Fact]
        public void Model_HasNoStoredProcedureMapping_OnSqlite()
        {
            // OnModelCreating only adds the sproc mappings when Database.IsSqlServer().
            var options = new DbContextOptionsBuilder<ChinookContext>().UseSqlite("Data Source=:memory:").Options;
            using var db = new ChinookContext(options);
            var track = db.Model.FindEntityType(typeof(Track))!;

            Assert.Null(track.GetInsertStoredProcedure());
            Assert.Null(track.GetUpdateStoredProcedure());
            Assert.Null(track.GetDeleteStoredProcedure());
        }

        [SqlServerFact]
        public async Task SaveChanges_InsertUpdateDelete_ExecuteTheMappedProcedures()
        {
            var log = new SqlLog();
            await using var db = CreateSqlServerContext(log);
            await using var tx = await db.Database.BeginTransactionAsync();
            try
            {
                // Insert -> sproc_InsertTrack, Id returned via OUTPUT parameter.
                var track = NewTrack();
                db.Tracks.Add(track);
                await db.SaveChangesAsync();

                Assert.True(track.Id > 0);
                Assert.Contains(log.Commands, c => c.Contains("EXEC") && c.Contains("sproc_InsertTrack"));
                Assert.DoesNotContain(log.Commands, c => c.Contains("INSERT INTO"));

                var inserted = await db.Tracks.AsNoTracking().SingleAsync(t => t.Id == track.Id);
                Assert.Equal("Sproc Mapping Test", inserted.Name);
                Assert.Equal(180000, inserted.Milliseconds);

                // Update -> sproc_UpdateTrack.
                log.Clear();
                track.Name = "Sproc Mapping Test (updated)";
                await db.SaveChangesAsync();

                Assert.Contains(log.Commands, c => c.Contains("EXEC") && c.Contains("sproc_UpdateTrack"));
                Assert.DoesNotContain(log.Commands, c => c.Contains("UPDATE [Track]"));
                var updatedName = await db.Tracks.AsNoTracking().Where(t => t.Id == track.Id).Select(t => t.Name).SingleAsync();
                Assert.Equal("Sproc Mapping Test (updated)", updatedName);

                // Delete -> sproc_DeleteTrack.
                log.Clear();
                db.Tracks.Remove(track);
                await db.SaveChangesAsync();

                Assert.Contains(log.Commands, c => c.Contains("EXEC") && c.Contains("sproc_DeleteTrack"));
                Assert.DoesNotContain(log.Commands, c => c.Contains("DELETE FROM [Track]"));
                Assert.False(await db.Tracks.AnyAsync(t => t.Id == track.Id));
            }
            finally
            {
                await tx.RollbackAsync();
            }
        }

        [SqlServerFact]
        public async Task SaveChanges_InsertGenre_UsesSprocAndRollsBack()
        {
            var log = new SqlLog();
            int newId;
            int countBefore;
            await using (var db = CreateSqlServerContext(log))
            {
                countBefore = await db.Genres.CountAsync();
                await using var tx = await db.Database.BeginTransactionAsync();
                var genre = new Genre { Name = "Sproc Mapping Test Genre" };
                db.Genres.Add(genre);
                await db.SaveChangesAsync();
                newId = genre.Id;

                Assert.True(newId > 0);
                Assert.Contains(log.Commands, c => c.Contains("sproc_InsertGenre") && c.Contains("OUTPUT"));
                Assert.Equal(countBefore + 1, await db.Genres.CountAsync());

                await tx.RollbackAsync();
            }

            // The rolled-back insert left no trace.
            await using (var db = CreateSqlServerContext())
            {
                Assert.Equal(countBefore, await db.Genres.CountAsync());
                Assert.False(await db.Genres.AnyAsync(g => g.Id == newId));
            }
        }

        [SqlServerFact]
        public async Task FromSql_ExecGetTrack_ReturnsEveryTrack()
        {
            await using var db = CreateSqlServerContext();

            var viaSproc = await db.Tracks.FromSql($"EXEC dbo.sproc_GetTrack").AsNoTracking().ToListAsync();
            var viaLinq = await db.Tracks.AsNoTracking().Select(t => t.Id).ToListAsync();

            Assert.NotEmpty(viaSproc);
            Assert.Equal(viaLinq.Order(), viaSproc.Select(t => t.Id).Order());
        }
    }
}
