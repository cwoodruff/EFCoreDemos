using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using query_tags.Model;

namespace query_tags;

public static class Program
{
    private static void Main()
    {
        using (var db = new AdventureWorksContext())
        {
            var workorders = db.WorkOrder
                .GroupBy(o => new {o.ProductID, o.ScrapReasonID})
                .Select(g => new
                {
                    g.Key.ProductID,
                    g.Key.ScrapReasonID,
                    Sum = g.Sum(o => o.ScrappedQty),
                    Min = g.Min(o => o.ScrappedQty),
                    Max = g.Max(o => o.ScrappedQty),
                    Avg = g.Average(o => o.ScrappedQty)
                })
                .TagWith("Description: WorkOrder Query from Query Tag demo")
                .TagWith("Query located: query_tags.Program.Main method")
                .TagWith(
                    @"Parameters:
                        None");

            foreach (var workorder in workorders)
            {
                Console.WriteLine(workorder.ProductID + ", " + workorder.Sum);
            }
        }

        Console.ReadLine();
    }
}