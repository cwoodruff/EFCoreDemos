using System;
using System.Linq;
using identity_resolution.Chinook;
using Microsoft.EntityFrameworkCore;

namespace identity_resolution;

public class Program
{
    static void Main()
    {
        using var db = new ChinookContext();

        var albumA = db.Albums.AsNoTrackingWithIdentityResolution().Single(e => e.Id == 1);
        var albumB = new Album { Id = 1, Title = "London Calling" };

        try
        {
            db.Update(albumB); // This will throw
        }
        catch (Exception e)
        {
            Console.WriteLine($"{e.GetType().FullName}: {e.Message}");
        }
    }
}