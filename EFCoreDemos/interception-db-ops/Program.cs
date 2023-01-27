using System;
using System.Linq;
using System.Threading.Tasks;
using interception_db_ops.Chinook;
using Microsoft.EntityFrameworkCore;

namespace interception_db_ops;

class Program
{
    static async Task Main(string[] args)
    {
        using (var db = new ChinookContext())
        {
            var tracks = db.Tracks.Where(t => t.GenreId == 1);

            await foreach (var track in tracks.AsAsyncEnumerable())
            {
                Console.WriteLine($"Found Tracks: {track.Name} {track.UnitPrice}");
            }
        }
    }
}