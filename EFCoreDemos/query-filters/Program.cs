using System;
using System.Linq;
using Demos.Chinook;
using Microsoft.EntityFrameworkCore;

#pragma warning disable 169

namespace Demos;

public class Program
{
    private static void Main()
    {
        using (var db = new ChinookContext())
        {
            var customers = db.Customers
                .Include(b => b.Invoices)
                .ToList();

            foreach (var customer in customers)
            {
                foreach (var invoice in customer.Invoices)
                {
                    Console.WriteLine($" - {invoice.CustomerId} [Date: {invoice.InvoiceDate}]");
                }

                Console.WriteLine();
            }
        }

        Console.ReadLine();
    }
}