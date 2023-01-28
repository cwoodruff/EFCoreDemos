using System;
using System.Collections.Generic;
using System.Linq;
using Demos.Chinook;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Demos;

public class Program
{
    private static void Main()
    {
        using (var db = new ChinookContext())
        {
            // Query with a DbFunction
            var counts = db.Invoices.Select(b => ChinookContext.ComputeInvoiceCount(b.CustomerId));

            foreach (var count in counts)
            {
                Console.WriteLine("Number of Invoices for Customer is " + count);
            }
        }

        Console.ReadLine();
    }
}