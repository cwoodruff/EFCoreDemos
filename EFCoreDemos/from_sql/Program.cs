using System;
using System.Data;
using from_sql.Model;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace from_sql;

class Program
{
    static void Main()
    {
        using var db = new AdventureWorksContext();

        string description = "Smith";
        SqlParameter parameter = new SqlParameter("LastName", SqlDbType.NVarChar);
        parameter.Direction = ParameterDirection.InputOutput;
        parameter.Size = description.Length;
        parameter.Value = description;

        var people =
            db.Person.FromSqlInterpolated($"SELECT * FROM [Person].[Person] WHERE LastName = {parameter}");
            
        foreach (var person in people)
        {
            Console.WriteLine($"Found Person: {person.FirstName} {person.LastName}");
        }
    }
}