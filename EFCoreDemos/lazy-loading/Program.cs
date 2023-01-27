using System;
using System.Linq;
using lazy_loading.Chinook;

namespace lazy_loading;

static class Program
{
    static void Main(string[] args)
    {
        using (var db = new ChinookContext())
        {
            var albums = db.Albums.ToList();

            foreach (var album in albums)
            {
                if (album.Artist != null)
                    Console.WriteLine(album.Title + " from " + album.Artist.Name);
            }
        }

        Console.ReadLine();
    }
}