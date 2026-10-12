using BenchmarkDotNet.Running;
using contains_collection_translation;
using contains_collection_translation.Benchmarks;
using contains_collection_translation.Chinook;
using Microsoft.EntityFrameworkCore;

// Default: show how ids.Contains(t.Id) is translated, then what each strategy does to the plan cache.
// Benchmark:  dotnet run -c Release -- benchmark
if (args.Length > 0 && args[0].Equals("benchmark", StringComparison.OrdinalIgnoreCase))
{
    BenchmarkRunner.Run<ContainsBenchmarks>(args: args[1..]);
    return;
}

var connectionString = ContainsBenchmarks.ConnectionString;

// A fresh id per run goes into every TagWith comment, so the plan-cache counts below only see this run.
var run = Guid.NewGuid().ToString("N")[..8];

await using var context = new ChinookContext(
    new DbContextOptionsBuilder<ChinookContext>().UseSqlServer(connectionString).Options);

Console.WriteLine("Where(t => ids.Contains(t.Id)) - how EF Core 10 turns a .NET list into SQL");
Console.WriteLine();

// 1. The EF 10 default: one parameter per value, padded to a bucket size.
Console.WriteLine("== 1. Default (EF 10: ParameterTranslationMode.MultipleParameters) ==");
foreach (var size in new[] { 3, 7, 10 })
{
    int[] ids = [.. Enumerable.Range(1, size)];
    var query = TrackQueries.ByIds(context, ids, Strategy.Default, run);
    Console.WriteLine($"-- {size} ids --");
    Console.WriteLine(WhereClause(query.ToQueryString()));
    Console.WriteLine($"-> {await query.CountAsync()} rows");
    Console.WriteLine();
}
Console.WriteLine("Each value is its own parameter. Past 5 values EF pads the list (repeating the last value)");
Console.WriteLine("up to a bucket size, so lists of 7 and 10 send the same SQL text - and share one cached plan.");
Console.WriteLine();
Console.WriteLine("List size -> parameters sent:");
var ladder = Enumerable.Range(1, 40)
    .Select(size => (size, parameters: ParameterCount(TrackQueries.ByIds(context, [.. Enumerable.Range(1, size)], Strategy.Default, run).ToQueryString())))
    .GroupBy(x => x.parameters, x => x.size)
    .Select(g => g.Count() == 1 ? $"{g.First()}->{g.Key}" : $"{g.First()}-{g.Last()}->{g.Key}");
Console.WriteLine("  " + string.Join(", ", ladder));
Console.WriteLine();

// 2. Per-query overrides.
int[] sample = [1, 2, 3];
Console.WriteLine("== 2. Per-query overrides ==");
Console.WriteLine("-- EF.Constant(ids).Contains(t.Id): values inlined into the SQL text --");
Console.WriteLine(WhereClause(TrackQueries.ByIds(context, sample, Strategy.Constant, run).ToQueryString()));
Console.WriteLine();
Console.WriteLine("-- EF.Parameter(ids).Contains(t.Id): one JSON parameter, unpacked with OPENJSON --");
Console.WriteLine(WhereClause(TrackQueries.ByIds(context, sample, Strategy.Parameter, run).ToQueryString()));
Console.WriteLine();

// 3. Changing the default for the whole context.
Console.WriteLine("== 3. Changing the default: UseSqlServer(..., o => o.UseParameterizedCollectionMode(...)) ==");
// EnableServiceProviderCaching(false): EF Core 10.0 doesn't count this option when deciding whether two contexts
// can share an internal service provider (and its query cache). Without it, this context would reuse the
// translation already compiled above. A real app sets the mode once, so it only matters when mixing modes.
await using (var jsonByDefault = new ChinookContext(new DbContextOptionsBuilder<ChinookContext>()
                 .EnableServiceProviderCaching(false)
                 .UseSqlServer(connectionString, o => o.UseParameterizedCollectionMode(ParameterTranslationMode.Parameter))
                 .Options))
{
    Console.WriteLine("-- ParameterTranslationMode.Parameter (the EF 8/9 behavior), plain ids.Contains(t.Id) --");
    Console.WriteLine(WhereClause(TrackQueries.ByIds(jsonByDefault, sample, Strategy.Default, run).ToQueryString()));
    Console.WriteLine();
}

// 4. What each strategy does to SQL Server's plan cache.
const int queries = 40;
Console.WriteLine($"== 4. Plan cache: {queries} queries per strategy, each with a different list (1 to {queries} ids) ==");
Console.WriteLine("Counting sys.dm_exec_cached_plans entries whose SQL text carries this run's TagWith comment.");
Console.WriteLine();
Console.WriteLine($"  {"Strategy",-34} {"Queries",7} {"Cached plans",12} {"Plan reuses",11}");

// Its own run id, so the COUNT queries executed in section 1 don't show up in these numbers.
var cacheRun = run + "-cache";
var random = new Random(42);
foreach (var strategy in Enum.GetValues<Strategy>())
{
    for (var size = 1; size <= queries; size++)
    {
        await TrackQueries.ByIds(context, TrackQueries.RandomIds(random, size), strategy, cacheRun).ToListAsync();
    }

    var tag = TrackQueries.Tag(cacheRun, strategy);
    var (plans, uses) = await PlanCache.CountAsync(connectionString, tag);
    Console.WriteLine($"  {Describe(strategy),-34} {queries,7} {plans,12} {uses - plans,11}");
}
Console.WriteLine();
Console.WriteLine("  EF.Constant   - every distinct list is a new SQL text: one compile and one cached plan each.");
Console.WriteLine("                  Real traffic (search filters, selected rows) fills the cache with single-use plans.");
Console.WriteLine("  Default       - one plan per padding bucket: a handful of plans, reused across list sizes.");
Console.WriteLine("  EF.Parameter  - one plan for every list, but SQL Server can't see the values or the row count");
Console.WriteLine("                  when it optimizes, so the plan is a guess (fine here, risky on skewed data).");
Console.WriteLine();

var evicted = await PlanCache.EvictAsync(connectionString, $"{TrackQueries.TagPrefix} {run}");
Console.WriteLine($"Cleanup: evicted this run's {evicted} plan(s) with DBCC FREEPROCCACHE(plan_handle); nothing else touched.");
Console.WriteLine();
Console.WriteLine("Hint: run `dotnet run -c Release -- benchmark` to execute the BenchmarkDotNet harness.");

static string Describe(Strategy strategy) => strategy switch
{
    Strategy.Default => "Default (MultipleParameters)",
    Strategy.Constant => "EF.Constant (inlined values)",
    Strategy.Parameter => "EF.Parameter (OPENJSON)",
    _ => strategy.ToString(),
};

static int ParameterCount(string sql) =>
    sql.Split('\n').Count(l => l.StartsWith("DECLARE", StringComparison.Ordinal));

// ToQueryString includes the DECLAREs and the tag comment; the WHERE line is the interesting part.
static string WhereClause(string sql)
{
    var lines = sql.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    var declares = lines.Where(l => l.StartsWith("DECLARE", StringComparison.Ordinal)).ToList();
    var where = lines.SkipWhile(l => !l.StartsWith("WHERE", StringComparison.Ordinal));
    var shownDeclares = declares.Count > 4 ? [.. declares.Take(3), $"... ({declares.Count} DECLAREs in total)"] : declares;
    return string.Join(Environment.NewLine, shownDeclares.Concat(where));
}
