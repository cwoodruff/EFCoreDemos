using System;
using System.Collections.Generic;
using System.Linq;
using keylessentitytypes.Chinook;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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