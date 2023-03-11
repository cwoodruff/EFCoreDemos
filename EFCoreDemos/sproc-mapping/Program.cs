using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using sproc_mapping.Chinook;

#pragma warning disable 169

namespace Demos;

public class Program
{
    private static void Main()
    {
        using (var db = new ChinookContext())
        {
            
        }

        Console.ReadLine();
    }
}