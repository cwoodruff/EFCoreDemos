# EF Core Pagination Benchmark

Companion code for the blog post **"Pagination in Entity Framework Core: Why `Skip`/`Take` Falls Apart on Hot Tables."**

Benchmarks offset pagination (`Skip`/`Take`) against keyset pagination (`Where` on the ordering key) using EF Core 10 against SQL Server LocalDB with a 1M-row table.

## Prerequisites

- .NET 10 SDK
- SQL Server LocalDB (ships with Visual Studio, or install [SqlLocalDB separately](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb))

If you want to point at a different SQL Server instance, update the `ConnectionString` constants in:

- `src/PaginationBenchmark.Seeder/Program.cs`
- `src/PaginationBenchmark.Benchmarks/PaginationBenchmarks.cs`

## Project layout

```
src/
  PaginationBenchmark.Data/         DbContext and Order entity
  PaginationBenchmark.Seeder/       Console app that creates the DB and bulk-inserts rows
  PaginationBenchmark.Benchmarks/   BenchmarkDotNet runner + the two pagination services
```

## Running

### 1. Seed the database

```bash
dotnet run -c Release --project src/PaginationBenchmark.Seeder
```

Defaults to 1,000,000 rows. Pass a different count as an argument:

```bash
dotnet run -c Release --project src/PaginationBenchmark.Seeder -- 500000
```

The seeder uses `SqlBulkCopy` and finishes in roughly 10–30 seconds on a typical dev machine. It also runs `UPDATE STATISTICS Orders WITH FULLSCAN` so the query optimizer has accurate cardinality estimates.

### 2. Run the benchmark

```bash
dotnet run -c Release --project src/PaginationBenchmark.Benchmarks
```

BenchmarkDotNet **requires** Release mode and will refuse to run otherwise.

Results land in `BenchmarkDotNet.Artifacts/` as markdown, CSV, and HTML — paste the markdown table directly into the blog post.

## What the benchmark measures

Both services fetch a 20-row page at varying depths (page 1, 100, 1,000, 10,000, 50,000). The difference between approaches is:

- **Offset** — `OrderBy(Id).Skip((page-1)*20).Take(20)` → SQL `OFFSET ... FETCH NEXT`
- **Keyset** — `Where(o => o.Id > afterId).OrderBy(Id).Take(20)` → SQL `WHERE Id > @cursor` with a clustered index seek

Both queries use `AsNoTracking()` since pagination endpoints are read-only.

## Expected shape

Offset times grow roughly linearly with page depth. Keyset times stay essentially flat. The first page (`PageNumber = 1`) is comparable for both — divergence appears as depth increases.

## Tweaking

- **Composite keyset (CreatedAt + Id):** the `IX_Orders_CreatedAt_Id` index is already configured in `AppDbContext.OnModelCreating`. Add a `KeysetPaginationService.GetPageAsync(DateTime?, int?, int)` overload and a matching benchmark method.
- **Smaller datasets:** the perf gap shrinks at smaller sizes. Try seeding 10K rows to see why offset is fine for small tables.
- **Different RDBMS:** swap `UseSqlServer` for `UseNpgsql` or `UsePomelo` and update the connection string. The shape of the results should be similar.

## Cleanup

```sql
DROP DATABASE PaginationBenchmark;
```
