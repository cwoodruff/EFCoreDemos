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

using Microsoft.EntityFrameworkCore;
using Xunit;
using System.Threading.Tasks;

namespace EFCoreDemos.Tests
{
    public class DatabaseHealthTests
    {
        private async Task VerifySqliteHealth(DbContext context)
        {
            using (context)
            {
                Assert.True(context.Database.IsSqlite(), $"{context.GetType().FullName} should be using SQLite.");
                
                var connectionString = context.Database.GetConnectionString();
                Assert.Contains("chinook.db", connectionString);
                
                await context.Database.OpenConnectionAsync();
                await context.Database.CloseConnectionAsync();
            }
        }

        private DbContextOptions<T> CreateOptions<T>() where T : DbContext
        {
            return new DbContextOptionsBuilder<T>()
                .UseSqlite("Data Source=chinook.db")
                .Options;
        }

        [Fact] public async Task CompiledModels_IsHealthy() => await VerifySqliteHealth(new compiled_models::compiled_models.Chinook.ChinookContext(CreateOptions<compiled_models::compiled_models.Chinook.ChinookContext>()));
        [Fact] public async Task CompiledQuery_IsHealthy() => await VerifySqliteHealth(new compiled_query::compiled_query.Chinook.ChinookContext(CreateOptions<compiled_query::compiled_query.Chinook.ChinookContext>()));
        [Fact] public async Task ContextPooling_IsHealthy() => await VerifySqliteHealth(new context_pooling::context_pooling.Chinook.ChinookContext(CreateOptions<context_pooling::context_pooling.Chinook.ChinookContext>()));
        
        [Fact] public async Task DbFunctions_IsHealthy() => await VerifySqliteHealth(new db_functions::Demos.Chinook.ChinookContext());
        [Fact] public async Task Demo4EntityCounters_IsHealthy() => await VerifySqliteHealth(new demo_4_entity_counters::Demos.Chinook.ChinookContext());
        [Fact] public async Task ExecuteUpdateExecuteDelete_IsHealthy() => await VerifySqliteHealth(new executeupdate_executedelete::executeupdate_executedelete.Chinook.ChinookContext());
        [Fact] public async Task FilteredInclude_IsHealthy() => await VerifySqliteHealth(new filtered_include::filtered_include.Chinook.ChinookContext());
        [Fact] public async Task FlexibleEntityMapping_IsHealthy() => await VerifySqliteHealth(new flexible_entity_mapping::flexible_entity_mapping.Chinook.ChinookContext());
        [Fact] public async Task FromSql_IsHealthy() => await VerifySqliteHealth(new from_sql::from_sql.Chinook.ChinookContext());
        [Fact] public async Task IdentityResolution_IsHealthy() => await VerifySqliteHealth(new identity_resolution::identity_resolution.Chinook.ChinookContext());
        [Fact] public async Task InterceptionDbOps_IsHealthy() => await VerifySqliteHealth(new interception_db_ops::interception_db_ops.Chinook.ChinookContext());
        [Fact] public async Task JsonColumns_IsHealthy() => await VerifySqliteHealth(new json_columns::json_columns.Chinook.ChinookContext());
        [Fact] public async Task KeylessEntityTypes_IsHealthy() => await VerifySqliteHealth(new keyless_entity_types::keylessentitytypes.Chinook.ChinookContext());
        [Fact] public async Task LazyLoading_IsHealthy() => await VerifySqliteHealth(new lazy_loading::lazy_loading.Chinook.ChinookContext());
        [Fact] public async Task Like_IsHealthy() => await VerifySqliteHealth(new like::Demos.Chinook.ChinookContext());
        [Fact] public async Task LinqGroupBy_IsHealthy() => await VerifySqliteHealth(new linq_groupby::linq_groupby.Chinook.ChinookContext());
        [Fact] public async Task ManyToMany_IsHealthy() => await VerifySqliteHealth(new many_to_many::many_to_many.BloggingContext());
        [Fact] public async Task QueryFilters_IsHealthy() => await VerifySqliteHealth(new query_filters::Demos.Chinook.ChinookContext());
        [Fact] public async Task QueryTags_IsHealthy() => await VerifySqliteHealth(new query_tags::query_tags.Chinook.ChinookContext());
        [Fact] public async Task Required1On1Dependants_IsHealthy() => await VerifySqliteHealth(new required_1_on_1_dependants::required_1_on_1_dependants.Chinook.ChinookContext());
        [Fact] public async Task SavedChangesInterceptionAuditing_Blogs_IsHealthy() => await VerifySqliteHealth(new savedchanges_interception_auditing::savedchanges_interception_auditing.BlogsContext());
        [Fact] public async Task SimpleLoggingImprovedDiagnostics_IsHealthy() => await VerifySqliteHealth(new simple_logging_improved_diagnostics::simple_logging_improved_diagnostics.Chinook.ChinookContext());
        [Fact] public async Task SplitQueries_IsHealthy() => await VerifySqliteHealth(new split_queries::split_queries.Chinook.ChinookContext());
        [Fact] public async Task SprocMapping_IsHealthy() => await VerifySqliteHealth(new sproc_mapping::sproc_mapping.Chinook.ChinookContext(CreateOptions<sproc_mapping::sproc_mapping.Chinook.ChinookContext>()));
        [Fact] public async Task TemporalTables_IsHealthy() => await VerifySqliteHealth(new temporal_tables::temporal_tables.Chinook.ChinookContext());

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
        public void Spatial_IsHealthy()
        {
            using (var context = new spatial::spatial.Models.WideWorldImportersContext())
            {
                Assert.NotNull(context);
            }
        }

        [Fact]
        public void SpatialDemoApi_IsHealthy()
        {
            // For the API, we manually provide options
            var options = new DbContextOptionsBuilder<SpatialDemo.Api.Data.AppDbContext>()
                .UseSqlServer("Server=localhost;Database=dummy;Integrated Security=True;TrustServerCertificate=True", x => x.UseNetTopologySuite())
                .Options;

            using (var context = new SpatialDemo.Api.Data.AppDbContext(options))
            {
                Assert.NotNull(context);
            }
        }
    }
}
