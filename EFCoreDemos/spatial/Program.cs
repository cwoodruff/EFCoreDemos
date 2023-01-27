using System;
using System.Linq;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using spatial.Models;

namespace spatial;

class Program
{
    static void Main(string[] args)
    {
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var currentLocation = geometryFactory.CreatePoint(new Coordinate(-85.7693607, 42.8613121));

        using (var db = new WideWorldImportersContext())
        {
            var nearestCity = db.Cities
                .OrderBy(c => c.Location.Distance(currentLocation))
                .FirstOrDefault();

            Console.WriteLine("Closest City is " + nearestCity?.CityName);
        }

        Console.ReadLine();
    }
}