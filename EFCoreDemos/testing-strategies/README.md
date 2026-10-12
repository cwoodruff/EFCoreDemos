# Testing strategies for EF Core

One small service (`Store/StoreService.cs`) and one test suite (`Scenarios.cs`), run unchanged against three
test databases. Every scenario asserts what **SQL Server** - the production database - actually does, so a
failing test is a bug that test double would let through to production.

```
dotnet test -c Release --logger "console;verbosity=normal"
```

The demo launcher runs exactly that and streams each test result. A non-zero exit is the expected outcome:
the failures *are* the demo.

## Results (EF Core 10.0.12)

| Scenario                                      | InMemory | SQLite in-memory | SQL Server (Testcontainers) |
|-----------------------------------------------|:--------:|:----------------:|:---------------------------:|
| Transaction rollback discards work            | **fail** | pass             | pass |
| Unique constraint is enforced                 | **fail** | pass             | pass |
| Foreign key is enforced                       | **fail** | pass             | pass |
| Max length is enforced (`nvarchar(50)`)       | **fail** | **fail**         | pass |
| Case-insensitive lookup (SQL Server collation)| **fail** | **fail**         | pass |
| Raw SQL, portable                             | **fail** | pass             | pass |
| Raw SQL, T-SQL dialect (`TOP`)                | **fail** | **fail**         | pass |
| `ExecuteUpdate`                               | **fail** | pass             | pass |

Why each one fails:

- **InMemory** isn't a database. It never sees SQL, so there are no constraints, no column lengths and no
  collation (strings compare as C# strings, case-sensitively). `FromSql` and `ExecuteUpdate` throw.
  Transactions throw by default; this suite ignores `TransactionIgnoredWarning`, as suites that test
  transactional code usually do - and then the rollback silently doesn't happen.
- **SQLite** is a real relational database, so constraints, transactions, portable SQL and `ExecuteUpdate`
  work. It misses what is SQL Server-specific: it ignores declared `TEXT` lengths, compares with a
  case-sensitive `BINARY` collation, and rejects T-SQL.

Not on the list: ordering by a `decimal` column, an old SQLite gap, works on SQLite with EF Core 10.

## Cost (measured on the demo laptop)

| Test double  | Setup per run                   | Per test |
|--------------|---------------------------------|----------|
| InMemory     | none                            | ~1-50 ms |
| SQLite       | none                            | ~5-20 ms |
| SQL Server   | container start, ~9-14 s        | ~0.4 s (creates and drops a database per test) |

## Guidance

Microsoft's recommendation, from [Choosing a testing strategy](https://learn.microsoft.com/ef/core/testing/choosing-a-testing-strategy):

1. Have good test coverage against your actual production database system.
2. If you need a test double, prefer the repository pattern (stub your data layer above EF Core).
3. If that isn't viable, consider SQLite in-memory.
4. **Avoid the in-memory provider for testing** - it is discouraged and only supported for legacy applications.

Testcontainers makes option 1 cheap: one container per test run, a fresh database per test.

## Keeping solution-wide test runs green

The failing tests are tagged `[Trait("Category", "ProviderGap")]`. Exclude them when running everything:

```
dotnet test EFCoreDemos.sln --filter "Category!=ProviderGap"
```

If Docker isn't running, the `SqlServer_` tests are skipped, not failed.

## Stage prep

Testcontainers pulls the SQL Server image on first use. Pull it before the talk:

```
docker pull mcr.microsoft.com/mssql/server:2025-latest
```
