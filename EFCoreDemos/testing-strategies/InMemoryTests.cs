using testing_strategies.Infrastructure;

namespace testing_strategies;

/// <summary>
/// EF Core's in-memory provider. Every scenario fails here: it isn't a relational database, so it can't
/// enforce constraints, roll back, run SQL or match SQL Server's collation.
/// </summary>
public class InMemoryTests : Scenarios
{
    protected override Task<TestStore> CreateStoreAsync() => Task.FromResult<TestStore>(new InMemoryStore());

    [Fact, Trait("Category", "ProviderGap")]
    public Task InMemory_Transaction_Rollback_DiscardsWork() => Transaction_Rollback_DiscardsWork();

    [Fact, Trait("Category", "ProviderGap")]
    public Task InMemory_UniqueConstraint_IsEnforced() => UniqueConstraint_IsEnforced();

    [Fact, Trait("Category", "ProviderGap")]
    public Task InMemory_ForeignKey_IsEnforced() => ForeignKey_IsEnforced();

    [Fact, Trait("Category", "ProviderGap")]
    public Task InMemory_MaxLength_IsEnforced() => MaxLength_IsEnforced();

    [Fact, Trait("Category", "ProviderGap")]
    public Task InMemory_CaseInsensitiveLookup_MatchesSqlServerCollation() => CaseInsensitiveLookup_MatchesSqlServerCollation();

    [Fact, Trait("Category", "ProviderGap")]
    public Task InMemory_RawSql_Portable_Works() => RawSql_Portable_Works();

    [Fact, Trait("Category", "ProviderGap")]
    public Task InMemory_RawSql_SqlServerDialect_Works() => RawSql_SqlServerDialect_Works();

    [Fact, Trait("Category", "ProviderGap")]
    public Task InMemory_ExecuteUpdate_AppliesInDatabase() => ExecuteUpdate_AppliesInDatabase();
}
