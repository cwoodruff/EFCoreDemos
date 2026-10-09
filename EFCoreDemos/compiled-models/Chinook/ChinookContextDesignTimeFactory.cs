using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace compiled_models.Chinook;

// Used only by `dotnet ef dbcontext optimize` to build the context at design time.
public class ChinookContextDesignTimeFactory : IDesignTimeDbContextFactory<ChinookContext>
{
    public ChinookContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ChinookContext>()
            .UseSqlite("Data Source=chinook.db")
            .Options);
}
