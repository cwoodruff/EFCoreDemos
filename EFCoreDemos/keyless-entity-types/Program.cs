using System;
using System.Linq;
using keylessentitytypes.Chinook;

namespace keylessentitytypes;

public class Program
{
    private static void Main()
    {
        using (var db = new ChinookContext())
        {
            var albumsWithArtistName = db.AlbumWithArtistName.ToList();

            foreach (var album in albumsWithArtistName)
            {
                Console.WriteLine($"{album.Name} has {album.Name} album.");
            }
        }
    }
}