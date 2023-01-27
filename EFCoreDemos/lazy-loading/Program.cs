using System;
using System.Linq;
using Performance.EFCore;

namespace Demos;

static class Program
{
    static void Main(string[] args)
    {
        using (var db = new AdventureWorksContext())
        {
            var customers = db.Customers.ToList();

            foreach (var customer in customers)
            {
                if (customer.Store != null && customer.Territory != null)
                    Console.WriteLine(customer.Store.Name + " located at " + customer.Territory.Name);
            }
        }

        Console.ReadLine();
    }
}