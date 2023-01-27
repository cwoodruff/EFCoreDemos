using System;
using System.Linq;
using linq_groupby.Model;

namespace Demos;

public class Program
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
                });

            foreach (var workorder in workorders)
            {
                Console.WriteLine("Product " + workorder.ProductID + ", Scrapped Sum " + workorder.Sum +
                                  " Scrapped Reason " + workorder.ScrapReasonID);
            }
        }

        Console.ReadLine();
    }
}