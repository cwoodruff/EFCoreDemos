extern alias contains_collection_translation;

using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using contains_collection_translation::contains_collection_translation;
using contains_collection_translation::contains_collection_translation.Benchmarks;
using contains_collection_translation::contains_collection_translation.Chinook;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// contains-collection-translation: ids.Contains(t.Id) under EF 10's default (one padded parameter per value),
    /// EF.Constant (inlined values) and EF.Parameter (one OPENJSON parameter), the context-wide
    /// UseParameterizedCollectionMode switch, and the plan-cache footprint of each. Read-only against Chinook;
    /// the plan-cache test evicts only its own tagged plans.
    /// </summary>
    public class ContainsCollectionTranslationTests
    {
        private static readonly int[] Ids = [1, 2, 3];

        private static ChinookContext CreateContext(ParameterTranslationMode? mode = null)
        {
            var builder = new DbContextOptionsBuilder<ChinookContext>();
            if (mode is not null)
            {
                // The mode isn't part of EF's service-provider cache key, so a context with a different mode
                // needs its own provider (and query cache) - see Program.cs.
                builder.EnableServiceProviderCaching(false);
            }

            builder.UseSqlServer(SqlServerTestServer.ConnectionString + "Database=Chinook;",
                o => { if (mode is { } m) o.UseParameterizedCollectionMode(m); });
            return new ChinookContext(builder.Options);
        }

        private static string Sql(ChinookContext db, int[] ids, Strategy strategy) =>
            TrackQueries.ByIds(db, ids, strategy, "test").ToQueryString();

        [Fact]
        public void Default_SendsOneParameterPerValue()
        {
            using var db = CreateContext();

            var sql = Sql(db, Ids, Strategy.Default);

            Assert.Contains("IN (@ids1, @ids2, @ids3)", sql);
            Assert.DoesNotContain("OPENJSON", sql);
        }

        [Fact]
        public void Default_PadsLargerListsToABucket_SoDifferentSizesShareOneSqlText()
        {
            using var db = CreateContext();

            var seven = Sql(db, [.. Enumerable.Range(1, 7)], Strategy.Default);
            var ten = Sql(db, [.. Enumerable.Range(1, 10)], Strategy.Default);

            Assert.Contains("@ids10", seven);
            Assert.DoesNotContain("@ids11", seven);
            Assert.Equal(StatementText(seven), StatementText(ten));
        }

        [Fact]
        public void EfConstant_InlinesTheValues()
        {
            using var db = CreateContext();

            var sql = Sql(db, Ids, Strategy.Constant);

            Assert.Contains("IN (1, 2, 3)", sql);
            Assert.DoesNotContain("DECLARE", sql);
        }

        [Fact]
        public void EfParameter_SendsOneJsonParameterUnpackedWithOpenJson()
        {
            using var db = CreateContext();

            var sql = Sql(db, Ids, Strategy.Parameter);

            Assert.Contains("DECLARE @ids nvarchar(4000) = N'[1,2,3]';", sql);
            Assert.Contains("OPENJSON(@ids)", sql);
        }

        [Theory]
        [InlineData(ParameterTranslationMode.Constant, "IN (1, 2, 3)")]
        [InlineData(ParameterTranslationMode.Parameter, "OPENJSON(@ids)")]
        [InlineData(ParameterTranslationMode.MultipleParameters, "IN (@ids1, @ids2, @ids3)")]
        public void UseParameterizedCollectionMode_ChangesTheDefaultForPlainContains(ParameterTranslationMode mode, string expected)
        {
            using var db = CreateContext(mode);

            Assert.Contains(expected, Sql(db, Ids, Strategy.Default));
        }

        [SqlServerFact]
        public async Task EveryStrategy_ReturnsTheSameRows()
        {
            await using var db = CreateContext();
            int[] ids = [.. TrackQueries.RandomIds(new Random(7), 50)];

            foreach (var strategy in Enum.GetValues<Strategy>())
            {
                var rows = await TrackQueries.ByIds(db, ids, strategy, "test").Select(t => t.Id).OrderBy(id => id).ToListAsync();
                Assert.Equal(ids, rows);
            }
        }

        [SqlServerFact]
        public async Task PlanCache_ConstantCompilesAPlanPerList_ParameterReusesOne()
        {
            var connectionString = SqlServerTestServer.ConnectionString + "Database=Chinook;";
            var run = "test-" + Guid.NewGuid().ToString("N")[..8];
            await using var db = CreateContext();

            try
            {
                foreach (var strategy in Enum.GetValues<Strategy>())
                {
                    for (var size = 1; size <= 12; size++)
                    {
                        await TrackQueries.ByIds(db, [.. Enumerable.Range(1, size)], strategy, run).ToListAsync();
                    }
                }

                var constant = await PlanCache.CountAsync(connectionString, TrackQueries.Tag(run, Strategy.Constant));
                var parameter = await PlanCache.CountAsync(connectionString, TrackQueries.Tag(run, Strategy.Parameter));
                var @default = await PlanCache.CountAsync(connectionString, TrackQueries.Tag(run, Strategy.Default));

                Assert.Equal(12, constant.Plans);  // one per distinct list
                Assert.Equal(1, parameter.Plans);  // one for all of them
                Assert.Equal(7, @default.Plans);   // sizes 1-5, then buckets 6-10 and 11-20
            }
            finally
            {
                await PlanCache.EvictAsync(connectionString, $"{TrackQueries.TagPrefix} {run}");
            }
        }

        [SqlServerFact]
        public void Benchmarks_SetupVerifiesAllStrategiesAgree()
        {
            var benchmarks = new ContainsBenchmarks { Size = 50 };
            benchmarks.Setup(); // throws if any strategy returns different rows
            benchmarks.Cleanup();
        }

        // The SQL without the DECLARE lines (whose values differ between lists).
        private static string StatementText(string sql) =>
            string.Join('\n', sql.Split('\n').Where(l => !l.StartsWith("DECLARE", StringComparison.Ordinal)));
    }
}
