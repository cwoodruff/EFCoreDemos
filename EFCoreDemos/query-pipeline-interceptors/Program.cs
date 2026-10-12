using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using query_pipeline_interceptors;
using query_pipeline_interceptors.Chinook;
using query_pipeline_interceptors.Interceptors;

var sqlLog = new List<string>();
var materialization = new RetrievedAtInterceptor(TimeProvider.System, new MinutesSecondsFormatter());
var ordering = new DefaultOrderingInterceptor();
var options = Demo.Options(Demo.ConnectionString, [materialization, ordering], sqlLog.Add);

Section("1. IMaterializationInterceptor: runs as each entity is created");
Console.WriteLine("""
    public object InitializedInstance(MaterializationInterceptionData data, object entity)
    {
        if (entity is IHasRetrievedAt stamped) stamped.RetrievedAt = clock.GetUtcNow();   // [NotMapped]
        if (entity is Track track) track.Formatter = formatter;                          // inject a service
        return entity;
    }
    """);

using (var db = new ChinookContext(options))
{
    var tracks = db.Tracks.Where(t => t.AlbumId == 1).OrderBy(t => t.Id).Take(4).ToList();
    Console.WriteLine($"  {"Track",-42} {"Length",6}  RetrievedAt");
    foreach (var track in tracks)
    {
        Console.WriteLine($"  {track.Name,-42} {track.Length,6}  {track.RetrievedAt:HH:mm:ss.fff}");
    }

    Console.WriteLine();
    Console.WriteLine("RetrievedAt and Formatter aren't columns. Length is computed by the injected IDurationFormatter,");
    Console.WriteLine("so the entity carries no formatting code and no service locator.");

    Console.WriteLine();
    Console.WriteLine("When does it fire? (calls counted per query)");
    PrintCalls("Tracking query, album 6", () => db.Tracks.Where(t => t.AlbumId == 6).ToList().Count);
    PrintCalls("AsNoTracking query, album 5", () => db.Tracks.AsNoTracking().Where(t => t.AlbumId == 5).ToList().Count);
    PrintCalls("Projection to new { t.Name }, album 5", () => db.Tracks.Where(t => t.AlbumId == 5).Select(t => new { t.Name }).ToList().Count);
    PrintCalls("Projection new { t, t.Album.Title }, album 7", () => db.Tracks.AsNoTracking().Where(t => t.AlbumId == 7).Select(t => new { t, t.Album!.Title }).ToList().Count);

    var firstLoad = db.Tracks.Single(t => t.Id == 1);
    var stamp = firstLoad.RetrievedAt;
    Thread.Sleep(20);
    PrintCalls("Same tracked query again, album 6", () => db.Tracks.Where(t => t.AlbumId == 6).ToList().Count);
    var again = db.Tracks.Single(t => t.Id == 1);
    Console.WriteLine();
    Console.WriteLine("Entities only: a projection of plain values creates no entity, so nothing to intercept.");
    Console.WriteLine("A tracked query for rows already in the change tracker hands back the existing instances -");
    Console.WriteLine($"no new entity, no call: track 1's RetrievedAt is still {again.RetrievedAt:HH:mm:ss.fff} "
                      + (ReferenceEquals(firstLoad, again) && again.RetrievedAt == stamp ? "(same instance, stale stamp)." : "(refreshed)."));
}

Section("2. IQueryExpressionInterceptor: rewrite the LINQ tree before EF translates it");
Console.WriteLine("""
    public Expression QueryCompilationStarting(Expression query, QueryExpressionEventData data)
        => AddDefaultOrdering(query, data.Context.Model);   // OrderBy(e => e.Id) if the query has none
    """);

using (var db = new ChinookContext(options))
{
    Console.WriteLine();
    Console.WriteLine("-- a) Paging with no OrderBy: Skip/Take over an unordered set is non-deterministic --");
    var page = db.Tracks.Where(t => t.AlbumId == 1).Skip(2).Take(3);
    var pageSql = page.ToQueryString();
    PrintRewrite(ordering);
    Console.WriteLine();
    Console.WriteLine(pageSql);
    Console.WriteLine($"-> {string.Join(", ", page.ToList().Select(t => $"#{t.Id}"))}");

    Console.WriteLine();
    Console.WriteLine("-- b) Already ordered: the author's OrderBy wins, nothing is added --");
    _ = db.Tracks.Where(t => t.AlbumId == 1).OrderBy(t => t.Name).Skip(2).Take(3).ToQueryString();
    PrintRewrite(ordering);

    Console.WriteLine();
    Console.WriteLine("-- c) Projection: the ordering goes below the Select, where the element is still a Track --");
    _ = db.Tracks.Where(t => t.AlbumId == 1).Select(t => new { t.Id, t.Name }).ToQueryString();
    PrintRewrite(ordering);

    Console.WriteLine();
    Console.WriteLine("-- d) First() and Last(): ordering added to the source --");
    sqlLog.Clear();
    var first = db.Tracks.First(t => t.Composer != null);
    PrintRewrite(ordering);
    Console.WriteLine(Demo.SqlOf(sqlLog.Last()));
    Console.WriteLine($"-> #{first.Id} {first.Name}");

    sqlLog.Clear();
    var last = db.Tracks.Where(t => t.AlbumId == 1).Last();
    Console.WriteLine(Demo.SqlOf(sqlLog.Last()));
    Console.WriteLine($"-> #{last.Id} {last.Name}  (EF turns Last() into ORDER BY ... DESC LIMIT 1)");
    using (var plain = new ChinookContext(Demo.Options(Demo.ConnectionString, [])))
    {
        try
        {
            plain.Tracks.Where(t => t.AlbumId == 1).Last();
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"Without the interceptor the same Last() throws: {ex.Message}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("-- e) Aggregates: Count/Any/Sum don't depend on order, so they're left alone --");
    _ = db.Tracks.Count(t => t.AlbumId == 1);
    PrintRewrite(ordering);
}

Section("3. Cost and caveats");
{
    // Fresh interceptors so the counts start at zero (see the last point below for why that also means fresh compiles).
    var counting = new DefaultOrderingInterceptor();
    var stamping = new RetrievedAtInterceptor(TimeProvider.System, new MinutesSecondsFormatter());
    using var db = new ChinookContext(Demo.Options(Demo.ConnectionString, [counting, stamping]));

    Console.WriteLine($"  {"Step",-58} {"Expression",10} {"Materialize",11}");
    void Step(string label, Action run)
    {
        var (e, m) = (counting.Calls, stamping.Calls);
        run();
        Console.WriteLine($"  {label,-58} {counting.Calls - e,10} {stamping.Calls - m,11}");
    }

    var albumId = 1;
    Step("ToQueryString() of a new query", () => db.Tracks.AsNoTracking().Where(t => t.AlbumId == albumId).Skip(1).Take(5).ToQueryString());
    Step("ToList() of the same query", () => db.Tracks.AsNoTracking().Where(t => t.AlbumId == albumId).Skip(1).Take(5).ToList());
    Step("Same query again", () => db.Tracks.AsNoTracking().Where(t => t.AlbumId == albumId).Skip(1).Take(5).ToList());
    albumId = 3;
    Step("Same query, captured variable changed (albumId = 3)", () => db.Tracks.AsNoTracking().Where(t => t.AlbumId == albumId).Skip(1).Take(5).ToList());
    Step("Same query, literal constant instead (AlbumId == 4)", () => db.Tracks.AsNoTracking().Where(t => t.AlbumId == 4).Skip(1).Take(5).ToList());
    Step("All 3,503 tracks, AsNoTracking", () => db.Tracks.AsNoTracking().ToList());
    Step("All 3,503 tracks again", () => db.Tracks.AsNoTracking().ToList());

    Console.WriteLine();
    Console.WriteLine("  - IQueryExpressionInterceptor runs when EF compiles a query, not when it executes one. The compiled");
    Console.WriteLine("    query is cached, so repeats and changed parameter values cost nothing. ToQueryString() compiles");
    Console.WriteLine("    on its own, and every distinct constant is a new query shape - one more compile, one more call.");
    Console.WriteLine("  - IMaterializationInterceptor runs once per entity instance, every time. Keep it to a few assignments:");
    Console.WriteLine("    a 10,000-row query calls it 10,000 times.");

    var compiles = Enumerable.Range(0, 3).Select(_ =>
    {
        var fresh = new DefaultOrderingInterceptor();
        using var ctx = new ChinookContext(Demo.Options(Demo.ConnectionString, [fresh]));
        ctx.Tracks.Where(t => t.AlbumId == 1).Skip(1).Take(5).ToList();
        return fresh.Calls;
    }).ToList();
    Console.WriteLine($"  - Reuse interceptor instances. Three contexts, each with a NEW interceptor instance, running a query");
    Console.WriteLine($"    EF has already compiled: {string.Join(", ", compiles)} compile(s) each. A new interceptor instance means new");
    Console.WriteLine("    options, so EF builds a new internal service provider - with an empty query cache - for it.");
    Console.WriteLine("    Create interceptors once (or register them as singletons) and pass the same instance every time.");
    Console.WriteLine("  - Only the top-level query is rewritten, and only while its element type is an entity with a key.");
    Console.WriteLine("    It's a safety net for paging, not a substitute for writing OrderBy: it changes every query in");
    Console.WriteLine("    the app silently, so a reader of the LINQ can't see the ORDER BY that ends up in the SQL.");
}

void PrintCalls(string label, Func<int> run)
{
    var before = materialization.Calls;
    var rows = run();
    Console.WriteLine($"  {label,-48} {rows,3} rows -> {materialization.Calls - before,3} calls");
}

static void PrintRewrite(DefaultOrderingInterceptor interceptor)
{
    if (interceptor.LastQuery is not { } last)
    {
        return;
    }

    Console.WriteLine("Before: " + Indent(Print(last.Before)));
    Console.WriteLine(last.Rewritten ? "After:  " + Indent(Print(last.After)) : "After:  (unchanged)");
}

static string Print(Expression expression) => new ExpressionPrinter().PrintExpression(expression);

static string Indent(string text) => text.Replace("\n", "\n        ");

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 90));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 90));
}
