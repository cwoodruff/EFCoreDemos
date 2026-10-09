extern alias compiled_models;
extern alias compiled_query;
extern alias context_pooling;
extern alias db_functions;
extern alias demo_4_entity_counters;
extern alias executeupdate_executedelete;
extern alias filtered_include;
extern alias flexible_entity_mapping;
extern alias from_sql;
extern alias identity_resolution;
extern alias interception_db_ops;
extern alias json_columns;
extern alias keyless_entity_types;
extern alias lazy_loading;
extern alias like;
extern alias linq_groupby;
extern alias many_to_many;
extern alias query_filters;
extern alias query_tags;
extern alias required_1_on_1_dependants;
extern alias savedchanges_interception_auditing;
extern alias simple_logging_improved_diagnostics;
extern alias spatial;
extern alias split_queries;
extern alias sproc_mapping;
extern alias temporal_tables;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Threading.Tasks;

namespace EFCoreDemos.Tests
{
    public class DatabaseHealthTests
    {
        // SQL Server (docker container sql2025) used by the spatial demos.
        private const string SqlServer =
            "Server=localhost,1433;User Id=sa;Password=8riwudeg!!;TrustServerCertificate=True;Connect Timeout=10;";

        // Tables that must exist (and be non-empty) in a real, seeded Chinook database.
        // The test project copies the seeded ../../Databases/chinook.db into its output folder (see csproj),
        // and the demo contexts use the relative "Data Source=chinook.db", which resolves against that folder.
        private static readonly string[] ChinookTables = ["Album", "Artist", "Track"];

        /// <summary>Verifies a context points at an existing, seeded Chinook SQLite database.</summary>
        private static Task VerifyChinookSqliteHealth(DbContext context) =>
            VerifySqliteHealth(context, requireExistingFile: true, requiredTables: ChinookTables);

        /// <summary>
        /// Verifies a SQLite-backed context. When <paramref name="requireExistingFile"/> is true the database file must
        /// already exist and is opened read-only, so SQLite can neither create an empty file nor modify the real one.
        /// Each table in <paramref name="requiredTables"/> must exist and contain at least one row.
        /// For a non-Chinook context, pass its own table names (e.g. "Blogs", "Posts") once its database file is settled.
        /// </summary>
        private static async Task VerifySqliteHealth(DbContext context, bool requireExistingFile = false, params string[] requiredTables)
        {
            using (context)
            {
                Assert.True(context.Database.IsSqlite(), $"{context.GetType().FullName} should be using SQLite.");

                var connectionString = context.Database.GetConnectionString();
                Assert.False(string.IsNullOrWhiteSpace(connectionString), $"{context.GetType().FullName} has no connection string.");

                if (requireExistingFile)
                {
                    var builder = new SqliteConnectionStringBuilder(connectionString);
                    var fullPath = Path.GetFullPath(builder.DataSource);
                    Assert.True(File.Exists(fullPath), $"SQLite database '{fullPath}' does not exist.");

                    builder.Mode = SqliteOpenMode.ReadOnly;
                    context.Database.SetConnectionString(builder.ToString());
                }

                await context.Database.OpenConnectionAsync();
                try
                {
                    foreach (var table in requiredTables)
                    {
                        var exists = await context.Database
                            .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM sqlite_master WHERE type = 'table' AND name = {table}")
                            .ToListAsync();
                        Assert.True(exists.Single() == 1, $"Table '{table}' is missing from {connectionString}.");

                        // Table names come from the constants above, never from user input.
                        var countSql = $"SELECT COUNT(*) AS Value FROM \"{table}\"";
                        var rows = await context.Database.SqlQueryRaw<int>(countSql).ToListAsync();
                        Assert.True(rows.Single() > 0, $"Table '{table}' in {connectionString} is empty.");
                    }
                }
                finally
                {
                    await context.Database.CloseConnectionAsync();
                }
            }
        }

        private DbContextOptions<T> CreateOptions<T>() where T : DbContext
        {
            return new DbContextOptionsBuilder<T>()
                .UseSqlite("Data Source=chinook.db")
                .Options;
        }

        [Fact] public async Task CompiledModels_IsHealthy() => await VerifyChinookSqliteHealth(new compiled_models::compiled_models.Chinook.ChinookContext(CreateOptions<compiled_models::compiled_models.Chinook.ChinookContext>()));
        [Fact] public async Task CompiledQuery_IsHealthy() => await VerifyChinookSqliteHealth(new compiled_query::compiled_query.Chinook.ChinookContext(CreateOptions<compiled_query::compiled_query.Chinook.ChinookContext>()));
        [Fact] public async Task ContextPooling_IsHealthy() => await VerifyChinookSqliteHealth(new context_pooling::context_pooling.Chinook.ChinookContext(CreateOptions<context_pooling::context_pooling.Chinook.ChinookContext>()));
        
        [Fact] public async Task DbFunctions_IsHealthy() => await VerifyChinookSqliteHealth(new db_functions::Demos.Chinook.ChinookContext(CreateOptions<db_functions::Demos.Chinook.ChinookContext>()));
        [Fact] public async Task Demo4EntityCounters_IsHealthy() => await VerifyChinookSqliteHealth(new demo_4_entity_counters::Demos.Chinook.ChinookContext());
        [Fact] public async Task ExecuteUpdateExecuteDelete_IsHealthy() => await VerifyChinookSqliteHealth(new executeupdate_executedelete::executeupdate_executedelete.Chinook.ChinookContext());
        [Fact] public async Task FilteredInclude_IsHealthy() => await VerifyChinookSqliteHealth(new filtered_include::filtered_include.Chinook.ChinookContext());
        [Fact] public async Task FlexibleEntityMapping_IsHealthy() => await VerifyChinookSqliteHealth(new flexible_entity_mapping::flexible_entity_mapping.Chinook.ChinookContext());
        [Fact] public async Task FromSql_IsHealthy() => await VerifyChinookSqliteHealth(new from_sql::from_sql.Chinook.ChinookContext());
        [Fact] public async Task IdentityResolution_IsHealthy() => await VerifyChinookSqliteHealth(new identity_resolution::identity_resolution.Chinook.ChinookContext());
        [Fact] public async Task InterceptionDbOps_IsHealthy() => await VerifyChinookSqliteHealth(new interception_db_ops::interception_db_ops.Chinook.ChinookContext());
        [Fact] public async Task JsonColumns_IsHealthy() => await VerifyChinookSqliteHealth(new json_columns::json_columns.Chinook.ChinookContext());
        [Fact] public async Task KeylessEntityTypes_IsHealthy() => await VerifyChinookSqliteHealth(new keyless_entity_types::keylessentitytypes.Chinook.ChinookContext(CreateOptions<keyless_entity_types::keylessentitytypes.Chinook.ChinookContext>()));
        [Fact] public async Task LazyLoading_IsHealthy() => await VerifyChinookSqliteHealth(new lazy_loading::lazy_loading.Chinook.ChinookContext());
        [Fact] public async Task Like_IsHealthy() => await VerifyChinookSqliteHealth(new like::Demos.Chinook.ChinookContext());
        [Fact] public async Task LinqGroupBy_IsHealthy() => await VerifyChinookSqliteHealth(new linq_groupby::linq_groupby.Chinook.ChinookContext());
        // Non-Chinook databases: no Chinook schema check (see VerifySqliteHealth to add their own tables).
        [Fact] public async Task ManyToMany_IsHealthy() => await VerifySqliteHealth(new many_to_many::many_to_many.BloggingContext());
        [Fact] public async Task QueryFilters_IsHealthy() => await VerifyChinookSqliteHealth(new query_filters::Demos.Chinook.ChinookContext());
        [Fact] public async Task QueryTags_IsHealthy() => await VerifyChinookSqliteHealth(new query_tags::query_tags.Chinook.ChinookContext());
        [Fact] public async Task Required1On1Dependants_IsHealthy() => await VerifyChinookSqliteHealth(new required_1_on_1_dependants::required_1_on_1_dependants.Chinook.ChinookContext());
        [Fact] public async Task SavedChangesInterceptionAuditing_Blogs_IsHealthy() => await VerifySqliteHealth(new savedchanges_interception_auditing::savedchanges_interception_auditing.BlogsContext());
        [Fact] public async Task SimpleLoggingImprovedDiagnostics_IsHealthy() => await VerifyChinookSqliteHealth(new simple_logging_improved_diagnostics::simple_logging_improved_diagnostics.Chinook.ChinookContext());
        [Fact] public async Task SplitQueries_IsHealthy() => await VerifyChinookSqliteHealth(new split_queries::split_queries.Chinook.ChinookContext());
        [Fact] public async Task SprocMapping_IsHealthy() => await VerifyChinookSqliteHealth(new sproc_mapping::sproc_mapping.Chinook.ChinookContext(CreateOptions<sproc_mapping::sproc_mapping.Chinook.ChinookContext>()));
        [Fact] public async Task TemporalTables_IsHealthy() => await VerifyChinookSqliteHealth(new temporal_tables::temporal_tables.Chinook.ChinookContext());

        [Fact]
        public void SprocMapping_SqlServer_IsHealthy()
        {
            using (var context = new sproc_mapping::sproc_mapping.Chinook.ChinookContext())
            {
                Assert.NotNull(context);
                Assert.True(context.Database.IsSqlServer());
            }
        }

        [Fact]
        public void DbFunctions_SqlServer_IsHealthy()
        {
            using (var context = new db_functions::Demos.Chinook.ChinookContext())
            {
                Assert.NotNull(context);
                Assert.True(context.Database.IsSqlServer());
            }
        }

        [Fact]
        public void KeylessEntityTypes_SqlServer_IsHealthy()
        {
            using (var context = new keyless_entity_types::keylessentitytypes.Chinook.ChinookContext())
            {
                Assert.NotNull(context);
                Assert.True(context.Database.IsSqlServer());
            }
        }

        [Fact]
        public async Task Spatial_IsHealthy()
        {
            // Uses the demo's own connection string (WideWorldImporters on the docker SQL Server).
            using (var context = new spatial::spatial.Models.WideWorldImportersContext())
            {
                Assert.True(context.Database.IsSqlServer());
                Assert.True(await context.Database.CanConnectAsync(), "Cannot connect to WideWorldImporters.");
                Assert.True(await context.Cities.AnyAsync(), "WideWorldImporters Application.Cities is empty.");
            }
        }

        [Fact]
        public async Task SpatialDemoApi_IsHealthy()
        {
            var options = new DbContextOptionsBuilder<SpatialDemo.Api.Data.AppDbContext>()
                .UseSqlServer(SqlServer + "Database=spatialdemo;", x => x.UseNetTopologySuite())
                .Options;

            using (var context = new SpatialDemo.Api.Data.AppDbContext(options))
            {
                Assert.True(await context.Database.CanConnectAsync(), "Cannot connect to spatialdemo.");
                Assert.Empty(await context.Database.GetPendingMigrationsAsync());
                Assert.True(await context.Locations.AnyAsync(), "spatialdemo Locations is empty.");
            }
        }
    }
}
