using dbcontext_lifetimes_di.Chinook;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Turn scope validation off so the captive-dependency anti-pattern blows up at
// runtime (when the captured context is used after disposal) rather than being
// caught at host startup. The runtime exception is the teaching moment.
builder.Host.UseDefaultServiceProvider(opts =>
{
    opts.ValidateScopes = false;
    opts.ValidateOnBuild = false;
});

const string connectionString = "Data Source=chinook.db";

// Three ways to register a DbContext, side by side - each on a distinct context type
// so AddDbContext's scoped DbContextOptions doesn't collide with the singleton factories.
builder.Services.AddDbContext<ScopedChinookContext>(o => o.UseSqlite(connectionString));
builder.Services.AddDbContextFactory<FactoryChinookContext>(o => o.UseSqlite(connectionString));
builder.Services.AddPooledDbContextFactory<PooledChinookContext>(o => o.UseSqlite(connectionString));

// The captive-dependency anti-pattern: a singleton that holds a scoped context.
builder.Services.AddSingleton<CaptiveSingletonService>();

var app = builder.Build();

app.MapGet("/", () => Results.Text("""
    Try:
      GET /scoped   - injected scoped DbContext (request-bound)
      GET /factory  - IDbContextFactory<T> (short-lived, per-call)
      GET /pooled   - PooledDbContextFactory<T> (reset on return)
      GET /captive  - Singleton captures a scoped DbContext (call twice - second call fails)
    """));

app.MapGet("/scoped", async (ScopedChinookContext db) =>
{
    var count = await db.Albums.CountAsync();
    return Results.Ok(new
    {
        lifetime = "scoped (request-bound)",
        contextHash = db.GetHashCode(),
        albumCount = count
    });
});

app.MapGet("/factory", async (IDbContextFactory<FactoryChinookContext> factory) =>
{
    await using var db = await factory.CreateDbContextAsync();
    var count = await db.Albums.CountAsync();
    return Results.Ok(new
    {
        lifetime = "factory (short-lived, per-call)",
        contextHash = db.GetHashCode(),
        albumCount = count
    });
});

app.MapGet("/pooled", async (IDbContextFactory<PooledChinookContext> pooledFactory) =>
{
    await using var db = await pooledFactory.CreateDbContextAsync();
    var count = await db.Albums.CountAsync();
    return Results.Ok(new
    {
        lifetime = "pooled (reset on return)",
        contextHash = db.GetHashCode(),
        albumCount = count
    });
});

app.MapGet("/captive", async (CaptiveSingletonService captive, ScopedChinookContext requestContext) =>
{
    var (isFirstCall, count) = await captive.CountAlbumsAsync(requestContext);
    return Results.Ok(new
    {
        lifetime = "singleton-captures-scoped",
        isFirstCall,
        albumCount = count,
        warning = "Call this endpoint a second time - the captured context is now disposed."
    });
});

app.Run();

// A singleton that captures the *first* request's scoped DbContext and then
// keeps using it across all subsequent requests. The captured context is
// disposed when the first request ends, so call #2 throws ObjectDisposedException.
public sealed class CaptiveSingletonService
{
    private ScopedChinookContext _captured;
    private readonly object _lock = new();

    public async Task<(bool IsFirstCall, int Count)> CountAlbumsAsync(ScopedChinookContext currentRequestContext)
    {
        bool firstCall;
        lock (_lock)
        {
            firstCall = _captured is null;
            _captured ??= currentRequestContext;
        }

        var count = await _captured.Albums.CountAsync();
        return (firstCall, count);
    }
}

// Marker subtypes so each registration owns its own DbContextOptions<T>.
public class ScopedChinookContext : ChinookContext
{
    public ScopedChinookContext(DbContextOptions<ScopedChinookContext> options) : base(options) { }
}

public class FactoryChinookContext : ChinookContext
{
    public FactoryChinookContext(DbContextOptions<FactoryChinookContext> options) : base(options) { }
}

public class PooledChinookContext : ChinookContext
{
    public PooledChinookContext(DbContextOptions<PooledChinookContext> options) : base(options) { }
}
