using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using testing_strategies.Store;

namespace testing_strategies.Infrastructure;

/// <summary>One empty database for one test. Hands out as many contexts on it as the test needs.</summary>
public abstract class TestStore : IAsyncDisposable
{
    protected abstract DbContextOptions<StoreContext> Options { get; }

    public StoreContext CreateContext() => new(Options);

    public virtual ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>
/// EF Core's in-memory provider: a dictionary per entity type, queried with LINQ to Objects.
/// Not a database - no SQL, no constraints, no transactions.
/// </summary>
public sealed class InMemoryStore : TestStore
{
    // One root for the whole run, so every store shares one internal EF service provider; the database
    // name keeps each test's data apart.
    private static readonly InMemoryDatabaseRoot Root = new();

    public InMemoryStore()
    {
        Options = new DbContextOptionsBuilder<StoreContext>()
            .UseInMemoryDatabase($"store-{Guid.NewGuid():N}", Root)
            // By default BeginTransaction throws on this provider. Suites that test code which uses
            // transactions switch that off - and from then on every transaction is silently ignored.
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
    }

    protected override DbContextOptions<StoreContext> Options { get; }
}

/// <summary>SQLite in-memory: a real relational database, alive as long as its connection stays open.</summary>
public sealed class SqliteStore : TestStore
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    private SqliteStore()
    {
        Options = new DbContextOptionsBuilder<StoreContext>().UseSqlite(_connection).Options;
    }

    protected override DbContextOptions<StoreContext> Options { get; }

    public static async Task<SqliteStore> CreateAsync()
    {
        var store = new SqliteStore();
        await store._connection.OpenAsync();
        await using var db = store.CreateContext();
        await db.Database.EnsureCreatedAsync();
        return store;
    }

    public override async ValueTask DisposeAsync() => await _connection.DisposeAsync();
}

/// <summary>The production engine: a fresh database per test inside the Testcontainers SQL Server.</summary>
public sealed class SqlServerStore : TestStore
{
    private SqlServerStore(string connectionString)
    {
        Options = new DbContextOptionsBuilder<StoreContext>().UseSqlServer(connectionString).Options;
    }

    protected override DbContextOptions<StoreContext> Options { get; }

    public static async Task<SqlServerStore> CreateAsync(SqlServerContainerFixture container)
    {
        var store = new SqlServerStore(container.ConnectionStringFor($"Store_{Guid.NewGuid():N}"));
        await using var db = store.CreateContext();
        await db.Database.EnsureCreatedAsync();
        return store;
    }

    public override async ValueTask DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }
}
