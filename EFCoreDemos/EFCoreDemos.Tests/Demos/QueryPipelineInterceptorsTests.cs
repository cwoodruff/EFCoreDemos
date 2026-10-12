extern alias query_pipeline_interceptors;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using query_pipeline_interceptors::query_pipeline_interceptors;
using query_pipeline_interceptors::query_pipeline_interceptors.Chinook;
using query_pipeline_interceptors::query_pipeline_interceptors.Interceptors;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// query-pipeline-interceptors: an IMaterializationInterceptor stamps a non-mapped RetrievedAt and injects a
    /// service into each entity as it is created; an IQueryExpressionInterceptor adds OrderBy(key) to top-level
    /// entity queries that have none. Also pins down when each one runs: per entity vs per query compilation.
    /// </summary>
    public class QueryPipelineInterceptorsTests : IDisposable
    {
        // Seeded Chinook: album 1 has 10 tracks (ids 1, 6-14), album 5 has 15, album 6 has 13.
        private const int Album1Tracks = 10;
        private const int Album5Tracks = 15;
        private const int Album6Tracks = 13;
        private const int AllTracks = 3503;

        private static readonly DateTimeOffset FixedNow = new(2026, 10, 12, 9, 30, 0, TimeSpan.Zero);

        private readonly ChinookCopy _db = new();
        private readonly RetrievedAtInterceptor _materialization = new(new FixedClock(FixedNow), new MinutesSecondsFormatter());
        private readonly DefaultOrderingInterceptor _ordering = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateContext(params IInterceptor[] interceptors) =>
            new(Demo.Options(_db.ConnectionString, interceptors));

        private ChinookContext CreateContext() => CreateContext(_materialization, _ordering);

        private static string OrderByClause(string sql) =>
            sql.Split('\n').Select(l => l.Trim()).SingleOrDefault(l => l.StartsWith("ORDER BY")) ?? "";

        [Fact]
        public void Materialization_StampsRetrievedAtAndInjectsTheFormatter()
        {
            using var db = CreateContext();

            var tracks = db.Tracks.Where(t => t.AlbumId == 1).ToList();
            var album = db.Albums.Single(a => a.Id == 1);

            Assert.Equal(Album1Tracks, tracks.Count);
            Assert.All(tracks, t =>
            {
                Assert.Equal(FixedNow, t.RetrievedAt);
                Assert.NotNull(t.Formatter);
            });
            Assert.Equal("5:43", tracks.Single(t => t.Id == 1).Length); // 343,719 ms
            Assert.Equal(FixedNow, album.RetrievedAt);
        }

        [Fact]
        public void Materialization_WithoutTheInterceptor_LeavesNonMappedMembersEmpty()
        {
            using var plain = CreateContext([]);

            var track = plain.Tracks.Single(t => t.Id == 1);

            Assert.Null(track.RetrievedAt);
            Assert.Equal("(no formatter)", track.Length);
        }

        [Fact]
        public void Materialization_RunsPerEntity_NotForPlainProjections_NorForAlreadyTrackedRows()
        {
            using var db = CreateContext();

            int Count(Action run)
            {
                var before = _materialization.Calls;
                run();
                return _materialization.Calls - before;
            }

            Assert.Equal(Album6Tracks, Count(() => db.Tracks.Where(t => t.AlbumId == 6).ToList()));
            Assert.Equal(Album5Tracks, Count(() => db.Tracks.AsNoTracking().Where(t => t.AlbumId == 5).ToList()));
            Assert.Equal(0, Count(() => db.Tracks.Where(t => t.AlbumId == 5).Select(t => new { t.Name }).ToList()));
            Assert.Equal(12, Count(() => db.Tracks.AsNoTracking().Where(t => t.AlbumId == 7)
                .Select(t => new { t, t.Album!.Title }).ToList()));

            // Album 6's tracks are already tracked: EF returns the same instances and creates nothing.
            Assert.Equal(0, Count(() => db.Tracks.Where(t => t.AlbumId == 6).ToList()));
        }

        [Fact]
        public void DefaultOrdering_AddsKeyOrderingToUnorderedPaging()
        {
            using var db = CreateContext();

            var query = db.Tracks.Where(t => t.AlbumId == 1).Skip(2).Take(3);
            var sql = query.ToQueryString();

            Assert.True(_ordering.LastQuery?.Rewritten);
            Assert.Equal("ORDER BY \"t\".\"Id\"", OrderByClause(sql));
            Assert.Equal([7, 8, 9], query.Select(t => t.Id).ToList());
        }

        [Fact]
        public void DefaultOrdering_LeavesAnExplicitOrderByAlone()
        {
            using var db = CreateContext();

            var sql = db.Tracks.Where(t => t.AlbumId == 1).OrderBy(t => t.Name).Skip(2).Take(3).ToQueryString();

            Assert.False(_ordering.LastQuery?.Rewritten);
            Assert.Equal("ORDER BY \"t\".\"Name\"", OrderByClause(sql));
        }

        [Fact]
        public void DefaultOrdering_GoesBelowAProjection()
        {
            using var db = CreateContext();

            var query = db.Tracks.Where(t => t.AlbumId == 1).Select(t => new { t.Id, t.Name });
            var sql = query.ToQueryString();

            Assert.True(_ordering.LastQuery?.Rewritten);
            Assert.Equal("ORDER BY \"t\".\"Id\"", OrderByClause(sql));
            Assert.Equal([1, 6, 7, 8, 9, 10, 11, 12, 13, 14], query.ToList().Select(r => r.Id));
        }

        [Fact]
        public void DefaultOrdering_MakesLastWork_WhichOtherwiseThrows()
        {
            using (var db = CreateContext())
            {
                Assert.Equal(14, db.Tracks.Where(t => t.AlbumId == 1).Last().Id);
                Assert.True(_ordering.LastQuery?.Rewritten);
            }

            using var plain = CreateContext([]);
            Assert.Throws<InvalidOperationException>(() => plain.Tracks.Where(t => t.AlbumId == 1).Last());
        }

        [Fact]
        public void DefaultOrdering_LeavesAggregatesAlone()
        {
            using var db = CreateContext();

            Assert.Equal(Album1Tracks, db.Tracks.Count(t => t.AlbumId == 1));
            Assert.False(_ordering.LastQuery?.Rewritten);

            Assert.True(db.Tracks.Any(t => t.AlbumId == 1));
            Assert.False(_ordering.LastQuery?.Rewritten);
        }

        [Fact]
        public void ExpressionInterceptor_RunsOncePerCompilation_NotPerExecution()
        {
            using var db = CreateContext();

            var albumId = 1;
            List<Track> Page() => db.Tracks.AsNoTracking().Where(t => t.AlbumId == albumId).Skip(1).Take(5).ToList();

            Page();
            Assert.Equal(1, _ordering.Calls);

            Page();
            Assert.Equal(1, _ordering.Calls); // cached

            albumId = 3;
            Page();
            Assert.Equal(1, _ordering.Calls); // new parameter value, same compiled query

            db.Tracks.AsNoTracking().Where(t => t.AlbumId == 4).Skip(1).Take(5).ToList();
            Assert.Equal(2, _ordering.Calls); // a literal constant is a new query shape

            var before = _materialization.Calls;
            db.Tracks.AsNoTracking().ToList();
            db.Tracks.AsNoTracking().ToList();
            Assert.Equal(3, _ordering.Calls);
            Assert.Equal(2 * AllTracks, _materialization.Calls - before); // ...but materialization runs every time
        }

        [Fact]
        public void ToQueryString_CompilesSeparatelyFromExecution()
        {
            using var db = CreateContext();

            var query = db.Tracks.AsNoTracking().Where(t => t.AlbumId == 1).Skip(1).Take(5);
            query.ToQueryString();
            query.ToList();

            Assert.Equal(2, _ordering.Calls);
        }

        [Fact]
        public void NewInterceptorInstance_DoesNotShareCompiledQueries()
        {
            var calls = Enumerable.Range(0, 3).Select(_ =>
            {
                var fresh = new DefaultOrderingInterceptor();
                using var db = CreateContext(fresh);
                db.Tracks.Where(t => t.AlbumId == 1).Skip(1).Take(5).ToList();
                return fresh.Calls;
            }).ToList();

            Assert.Equal([1, 1, 1], calls);
        }

        private sealed class FixedClock(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
