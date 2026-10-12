namespace multitenancy.Model;

/// <summary>A row that belongs to one tenant. Shared-database mode tells tenants apart by <see cref="TenantId"/>.</summary>
public class Project
{
    public int Id { get; set; }
    public string TenantId { get; set; } = "";
    public string Name { get; set; } = "";
}
