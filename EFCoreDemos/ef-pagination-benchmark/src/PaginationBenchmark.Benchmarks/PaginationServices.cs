using Microsoft.EntityFrameworkCore;
using PaginationBenchmark.Data;

namespace PaginationBenchmark.Benchmarks;

/// <summary>
/// Classic offset pagination using Skip/Take. Performance degrades linearly with offset depth.
/// </summary>
public sealed class OffsetPaginationService
{
    private readonly AppDbContext _db;
    public OffsetPaginationService(AppDbContext db) => _db = db;

    public Task<List<Order>> GetPageAsync(int pageNumber, int pageSize, CancellationToken ct = default)
    {
        return _db.Orders
            .AsNoTracking()
            .OrderBy(o => o.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }
}

/// <summary>
/// Keyset (seek) pagination. Uses a Where clause on the ordering key instead of skipping rows.
/// Performance is constant regardless of how deep into the dataset the caller is.
/// </summary>
public sealed class KeysetPaginationService
{
    private readonly AppDbContext _db;
    public KeysetPaginationService(AppDbContext db) => _db = db;

    public Task<List<Order>> GetPageAsync(int? afterId, int pageSize, CancellationToken ct = default)
    {
        var query = _db.Orders.AsNoTracking().AsQueryable();

        if (afterId.HasValue)
            query = query.Where(o => o.Id > afterId.Value);

        return query
            .OrderBy(o => o.Id)
            .Take(pageSize)
            .ToListAsync(ct);
    }
}
