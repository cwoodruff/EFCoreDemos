extern alias ef_vs_dapper_vs_ado;

using Dapper;
using EFCoreDemos.Tests.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ef_vs_dapper_vs_ado::ef_vs_dapper_vs_ado.Chinook;
using DemoProgram = ef_vs_dapper_vs_ado::ef_vs_dapper_vs_ado.Program;
using ReadBenchmarks = ef_vs_dapper_vs_ado::ef_vs_dapper_vs_ado.ReadBenchmarks;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// ef-vs-dapper-vs-ado: the same "Track by Name" read through EF Core, Dapper and raw ADO.NET against
    /// the Chinook SQLite database. A fair benchmark requires all three to return the same row.
    /// </summary>
    public class EfVsDapperVsAdoTests
    {
        private static void AssertSameTrack(Track expected, Track? actual, string approach)
        {
            Assert.True(actual != null, $"{approach} returned no track.");
            Assert.Equal(expected.Id, actual.Id);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.AlbumId, actual.AlbumId);
            Assert.Equal(expected.MediaTypeId, actual.MediaTypeId);
            Assert.Equal(expected.GenreId, actual.GenreId);
            Assert.Equal(expected.Composer, actual.Composer);
            Assert.Equal(expected.Milliseconds, actual.Milliseconds);
            Assert.Equal(expected.Bytes, actual.Bytes);
            Assert.Equal(expected.UnitPrice, actual.UnitPrice);
        }

        private static Track? ReadWithAdo(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = DemoProgram.TrackSql;
            cmd.Parameters.AddWithValue("@name", DemoProgram.TrackName);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;
            return new Track
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                AlbumId = reader.GetInt32(2),
                MediaTypeId = reader.GetInt32(3),
                GenreId = reader.GetInt32(4),
                Composer = reader.IsDBNull(5) ? null! : reader.GetString(5),
                Milliseconds = reader.GetInt32(6),
                Bytes = reader.IsDBNull(7) ? 0 : reader.GetInt32(7),
                UnitPrice = reader.GetDecimal(8)
            };
        }

        [Fact]
        public void Context_UsesSqlite()
        {
            using var db = new ChinookContext();
            Assert.True(db.Database.IsSqlite());
        }

        [Fact]
        public void EfQuery_EmitsLimit1_LikeTheHandWrittenSql()
        {
            using var db = new ChinookContext();

            var sql = db.Tracks.AsNoTracking().Where(t => t.Name == DemoProgram.TrackName).Take(1).ToQueryString();

            Assert.Contains("LIMIT", sql);
            Assert.Contains("LIMIT 1", DemoProgram.TrackSql);
        }

        [Fact]
        public void EfDapperAndAdo_ReturnTheSameTrack()
        {
            using var copy = new ChinookCopy();

            Track? ef;
            using (var db = copy.Redirect(new ChinookContext()))
            {
                ef = db.Tracks.AsNoTracking().FirstOrDefault(t => t.Name == DemoProgram.TrackName);
            }

            Assert.NotNull(ef);
            Assert.Equal(DemoProgram.TrackName, ef.Name);
            Assert.Equal(8, ef.Id);

            using var connection = new SqliteConnection(copy.ConnectionString);
            connection.Open();

            var dapper = connection.QueryFirstOrDefault<Track>(DemoProgram.TrackSql, new { name = DemoProgram.TrackName });
            var ado = ReadWithAdo(connection);

            AssertSameTrack(ef, dapper, "Dapper");
            AssertSameTrack(ef, ado, "ADO.NET");
        }

        [Fact]
        public void AllThreeApproaches_ReturnNothingForUnknownName()
        {
            using var copy = new ChinookCopy();
            const string missing = "No Such Track 9f1c";

            using (var db = copy.Redirect(new ChinookContext()))
            {
                Assert.Null(db.Tracks.AsNoTracking().FirstOrDefault(t => t.Name == missing));
            }

            using var connection = new SqliteConnection(copy.ConnectionString);
            connection.Open();
            Assert.Null(connection.QueryFirstOrDefault<Track>(DemoProgram.TrackSql, new { name = missing }));

            using var cmd = connection.CreateCommand();
            cmd.CommandText = DemoProgram.TrackSql;
            cmd.Parameters.AddWithValue("@name", missing);
            using var reader = cmd.ExecuteReader();
            Assert.False(reader.Read());
        }

        [Fact]
        public void ReadBenchmarks_EfCoreDapperAndAdoMethods_ReturnTheSameTrack()
        {
            // ReadBenchmarks uses the relative "Data Source=chinook.db", i.e. the seeded copy in the test
            // output folder (same assumption as DatabaseHealthTests). It only reads.
            var benchmarks = new ReadBenchmarks();
            benchmarks.Setup();
            try
            {
                var ef = benchmarks.EfCore();
                Assert.NotNull(ef);
                AssertSameTrack(ef, benchmarks.Dapper(), "Dapper");
                AssertSameTrack(ef, benchmarks.Ado(), "ADO.NET");
            }
            finally
            {
                benchmarks.Cleanup();
            }
        }
    }
}
