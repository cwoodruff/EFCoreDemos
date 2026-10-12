using testing_strategies.Infrastructure;

namespace testing_strategies;

/// <summary>
/// SQLite in-memory: a real relational database, so constraints, transactions, raw SQL and ExecuteUpdate
/// work. What it misses is everything that is specific to SQL Server.
/// </summary>
public class SqliteTests : Scenarios
{
    protected override async Task<TestStore> CreateStoreAsync() => await SqliteStore.CreateAsync();

    [Fact]
    public Task Sqlite_Transaction_Rollback_DiscardsWork() => Transaction_Rollback_DiscardsWork();

    [Fact]
    public Task Sqlite_UniqueConstraint_IsEnforced() => UniqueConstraint_IsEnforced();

    [Fact]
    public Task Sqlite_ForeignKey_IsEnforced() => ForeignKey_IsEnforced();

    // SQLite ignores the declared length of a TEXT column.
    [Fact, Trait("Category", "ProviderGap")]
    public Task Sqlite_MaxLength_IsEnforced() => MaxLength_IsEnforced();

    // SQLite compares with BINARY collation (case-sensitive); SQL Server's default is case-insensitive.
    [Fact, Trait("Category", "ProviderGap")]
    public Task Sqlite_CaseInsensitiveLookup_MatchesSqlServerCollation() => CaseInsensitiveLookup_MatchesSqlServerCollation();

    [Fact]
    public Task Sqlite_RawSql_Portable_Works() => RawSql_Portable_Works();

    // TOP is T-SQL; SQLite wants LIMIT.
    [Fact, Trait("Category", "ProviderGap")]
    public Task Sqlite_RawSql_SqlServerDialect_Works() => RawSql_SqlServerDialect_Works();

    [Fact]
    public Task Sqlite_ExecuteUpdate_AppliesInDatabase() => ExecuteUpdate_AppliesInDatabase();
}
