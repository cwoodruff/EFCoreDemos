using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Sqlite.Design.Internal;
using Microsoft.Extensions.DependencyInjection;

namespace shadow_properties_backing_fields.Model;

/// <summary>
/// Scaffolds the initial migration in-process: the same code `dotnet ef migrations add Initial` writes,
/// without installing the dotnet-ef tool or writing files.
/// </summary>
public static class MigrationPreview
{
    public static ScaffoldedMigration ScaffoldInitial(DbContext context)
    {
        var services = new ServiceCollection()
            .AddEntityFrameworkDesignTimeServices()
            .AddDbContextDesignTimeServices(context);

#pragma warning disable EF1001 // Internal namespace, but it's the same provider hook the dotnet-ef tool uses.
        new SqliteDesignTimeServices().ConfigureDesignTimeServices(services);
#pragma warning restore EF1001

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IMigrationsScaffolder>()
            .ScaffoldMigration("Initial", "shadow_properties_backing_fields.Migrations");
    }
}
