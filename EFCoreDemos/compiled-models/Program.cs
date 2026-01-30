using compiled_models.Chinook;
using Microsoft.EntityFrameworkCore;

public class Program
{
    private static ChinookContext? _context;

    private static void Main()
    {
        var builder = new DbContextOptionsBuilder<ChinookContext>();
        builder.UseSqlite("Data Source=chinook.db");

        var dbContextOptions = builder.Options;
        _context = new ChinookContext(dbContextOptions);

        // Warm up
        var artist = _context.Artists.First();
    }
}