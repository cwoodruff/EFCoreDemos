namespace multitenancy.Tenancy;

/// <summary>The current request's tenant. Registered as a scoped service, so every request has its own.</summary>
public interface ITenantProvider
{
    string? TenantId { get; }
}

/// <summary>
/// Set once per request by whatever identifies the tenant (host name, header, token claim) - in an
/// ASP.NET Core app that's middleware; in this demo it's <see cref="Requests.InTenantAsync{T}"/>.
/// </summary>
public sealed class TenantProvider : ITenantProvider
{
    public string? TenantId { get; set; }
}

/// <summary>Database-per-tenant: which database each tenant lives in.</summary>
public sealed class TenantCatalog(IReadOnlyDictionary<string, string> connectionStrings)
{
    public IReadOnlyDictionary<string, string> ConnectionStrings { get; } = connectionStrings;

    public string ConnectionStringFor(string? tenantId) =>
        tenantId != null && ConnectionStrings.TryGetValue(tenantId, out var connectionString)
            ? connectionString
            : throw new InvalidOperationException($"Unknown tenant '{tenantId ?? "(none)"}'.");
}
