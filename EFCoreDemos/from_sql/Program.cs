using System;
using System.Data;
using from_sql.Chinook;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace from_sql;

class Program
{
    static void Main()
    {
        using var db = new ChinookContext();

        string country = "Canada";
        SqlParameter parameter = new SqlParameter("Country", SqlDbType.NVarChar);
        parameter.Direction = ParameterDirection.InputOutput;
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