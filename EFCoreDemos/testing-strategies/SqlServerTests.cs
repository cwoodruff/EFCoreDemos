using testing_strategies.Infrastructure;

namespace testing_strategies;

/// <summary>
/// SQL Server in a Testcontainers container: the production engine, so the reference column - every scenario
/// passes here by definition. The price is Docker and a container start (seconds) per test run.
/// </summary>
[Collection(SqlServerCollection.Name)]
public class SqlServerTests(SqlServerContainerFixture container) : Scenarios
{
    protected override async Task<TestStore> CreateStoreAsync() => await SqlServerStore.CreateAsync(container);

    [SqlServerContainerFact]
    public Task SqlServer_Transaction_Rollback_DiscardsWork() => Transaction_Rollback_DiscardsWork();

    [SqlServerContainerFact]
    public Task SqlServer_UniqueConstraint_IsEnforced() => UniqueConstraint_IsEnforced();

    [SqlServerContainerFact]
    public Task SqlServer_ForeignKey_IsEnforced() => ForeignKey_IsEnforced();

    [SqlServerContainerFact]
    public Task SqlServer_MaxLength_IsEnforced() => MaxLength_IsEnforced();

    [SqlServerContainerFact]
    public Task SqlServer_CaseInsensitiveLookup_MatchesSqlServerCollation() => CaseInsensitiveLookup_MatchesSqlServerCollation();

    [SqlServerContainerFact]
    public Task SqlServer_RawSql_Portable_Works() => RawSql_Portable_Works();

    [SqlServerContainerFact]
    public Task SqlServer_RawSql_SqlServerDialect_Works() => RawSql_SqlServerDialect_Works();

    [SqlServerContainerFact]
    public Task SqlServer_ExecuteUpdate_AppliesInDatabase() => ExecuteUpdate_AppliesInDatabase();
}
