extern alias compiled_models;

using compiled_models::compiled_models.Chinook;
using compiled_models::compiled_models.CompiledModels;
using EFCoreDemos.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;
using RuntimeModelChinookContext = compiled_models::RuntimeModelChinookContext;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// compiled-models: UseModel(ChinookContextModel.Instance) makes the context use the pre-generated
    /// (dotnet ef dbcontext optimize) model instead of running OnModelCreating, while a context type the
    /// compiled model is not registered for (RuntimeModelChinookContext) builds the model at runtime.
    /// Both must describe the same model and return the same data.
    /// </summary>
    public class CompiledModelsTests : IDisposable
    {
        private readonly ChinookCopy _db = new();

        public void Dispose() => _db.Dispose();

        private ChinookContext CreateCompiled() =>
            new(new DbContextOptionsBuilder<ChinookContext>()
                .UseSqlite(_db.ConnectionString)
                .UseModel(ChinookContextModel.Instance)
                .Options);

        private ChinookContext CreateRuntime() =>
            new RuntimeModelChinookContext(new DbContextOptionsBuilder<ChinookContext>()
                .UseSqlite(_db.ConnectionString)
                .Options);

        [Fact]
        public void UseModel_ContextUsesTheCompiledModelInstance()
        {
            using var db = CreateCompiled();

            Assert.IsType<ChinookContextModel>(db.Model);
            Assert.Same(ChinookContextModel.Instance, db.Model);
        }

        [Fact]
        public void WithoutUseModel_AssemblyAttributeStillSelectsTheCompiledModel()
        {
            // [assembly: DbContextModel(typeof(ChinookContext), typeof(ChinookContextModel))] registers it.
            using var db = new ChinookContext(_db.Options<ChinookContext>());

            Assert.IsType<ChinookContextModel>(db.Model);
        }

        [Fact]
        public void RuntimeModelContext_BuildsItsModelFromOnModelCreating()
        {
            using var db = CreateRuntime();

            Assert.IsNotType<ChinookContextModel>(db.Model);
            Assert.IsAssignableFrom<RuntimeModel>(db.Model);
            Assert.NotSame(ChinookContextModel.Instance, db.Model);
        }

        [Fact]
        public void CompiledModel_IsInSyncWithOnModelCreating()
        {
            using var compiled = CreateCompiled();
            using var runtime = CreateRuntime();

            var compiledShape = Describe(compiled.Model);
            var runtimeShape = Describe(runtime.Model);

            Assert.Equal(11, compiled.Model.GetEntityTypes().Count());
            Assert.Equal(runtimeShape, compiledShape);
        }

        [Fact]
        public void CompiledModel_AndRuntimeModel_ReturnTheSameData()
        {
            using var compiled = CreateCompiled();
            using var runtime = CreateRuntime();

            Assert.Equal(Artists(runtime), Artists(compiled));
            Assert.Equal(AlbumsPerArtist(runtime), AlbumsPerArtist(compiled));
            Assert.Equal(runtime.Tracks.Count(), compiled.Tracks.Count());
            Assert.Equal(3503, compiled.Tracks.Count());

            // The same first query the demo times.
            Assert.Equal(
                runtime.Artists.AsNoTracking().OrderBy(a => a.Id).First().Name,
                compiled.Artists.AsNoTracking().OrderBy(a => a.Id).First().Name);
        }

        [Fact]
        public void CompiledModel_SupportsManyToManySkipNavigations()
        {
            using var compiled = CreateCompiled();
            using var runtime = CreateRuntime();

            var compiledCounts = compiled.Playlists.OrderBy(p => p.Id).Select(p => p.Tracks!.Count).ToList();
            var runtimeCounts = runtime.Playlists.OrderBy(p => p.Id).Select(p => p.Tracks!.Count).ToList();

            Assert.Equal(runtimeCounts, compiledCounts);
            Assert.True(compiledCounts.Sum() > 0);
        }

        private static List<(int, string?)> Artists(ChinookContext db) =>
            db.Artists.AsNoTracking().OrderBy(a => a.Id).Take(25).Select(a => new { a.Id, a.Name })
                .AsEnumerable().Select(a => (a.Id, a.Name)).ToList();

        private static List<(int, int)> AlbumsPerArtist(ChinookContext db) =>
            db.Artists.OrderBy(a => a.Id).Select(a => new { a.Id, Count = a.Albums.Count })
                .AsEnumerable().Select(a => (a.Id, a.Count)).ToList();

        private static List<string> Describe(IModel model)
        {
            var lines = new List<string>();

            foreach (var entityType in model.GetEntityTypes().OrderBy(e => e.Name))
            {
                lines.Add($"entity {entityType.Name} clr={entityType.ClrType.Name}");

                lines.AddRange(entityType.GetProperties()
                    .Select(p => $"  prop {entityType.Name}.{p.Name}:{p.ClrType.Name} null={p.IsNullable} max={p.GetMaxLength()}")
                    .Order());

                lines.AddRange(entityType.GetKeys()
                    .Select(k => $"  key {entityType.Name}({string.Join(",", k.Properties.Select(p => p.Name))}) pk={k.IsPrimaryKey()}")
                    .Order());

                lines.AddRange(entityType.GetForeignKeys()
                    .Select(fk => $"  fk {entityType.Name}({string.Join(",", fk.Properties.Select(p => p.Name))})"
                                  + $" -> {fk.PrincipalEntityType.Name} required={fk.IsRequired} delete={fk.DeleteBehavior}")
                    .Order());

                lines.AddRange(entityType.GetNavigations()
                    .Select(n => $"  nav {entityType.Name}.{n.Name} -> {n.TargetEntityType.Name} collection={n.IsCollection}")
                    .Order());

                lines.AddRange(entityType.GetSkipNavigations()
                    .Select(n => $"  skip {entityType.Name}.{n.Name} -> {n.TargetEntityType.Name}")
                    .Order());
            }

            foreach (var table in model.GetRelationalModel().Tables.OrderBy(t => t.Name))
            {
                lines.Add($"table {table.Schema}.{table.Name}");
                lines.AddRange(table.Columns
                    .Select(c => $"  column {table.Name}.{c.Name} type={c.StoreType} null={c.IsNullable}")
                    .Order());
            }

            return lines;
        }
    }
}
