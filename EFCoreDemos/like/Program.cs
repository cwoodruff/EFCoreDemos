using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Chinook;
using Microsoft.EntityFrameworkCore;

namespace Demos;

public class Program
{
    private static void Main()
    {
        using (var db = new ChinookContext())
        {
            var service = new ArtistService(db);
            var artists = service.SearchBlogs("the ");

            foreach (var artist in artists)
            {
                Console.WriteLine(artist.Name);
            }
        }

        Console.ReadLine();
    }
}

public class ArtistService
{
    private readonly ChinookContext _db;

    public ArtistService(ChinookContext db)
    {
        _db = db;
    }

    public IEnumerable<Artist> SearchBlogs(string term)
    {
        var likeExpression = $"%{term}%";

        return _db.Artists.Where(a => EF.Functions.Like(a.Name, likeExpression));
    }
}