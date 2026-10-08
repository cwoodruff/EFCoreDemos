using System;
using from_sql.Chinook;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace from_sql;

class Program
{
    static void Main()
    {
        using var db = new ChinookContext();

        string country = "Canada";
        // The context uses SQLite, so the explicit DbParameter must be a SqliteParameter
        // (a SqlClient SqlParameter throws InvalidCastException here).
        SqliteParameter parameter = new SqliteParameter("Country", SqliteType.Text);
        parameter.Size = country.Length;
        parameter.Value = country;

        var customers =
            db.Customers.FromSqlInterpolated($"SELECT * FROM [Customer] WHERE Country = {parameter}");

        foreach (var customer in customers)
        {
            Console.WriteLine($"Found Person: {customer.FirstName} {customer.LastName}");
        }
    }
}