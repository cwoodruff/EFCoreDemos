using left_right_join;
using left_right_join.Chinook;
using Microsoft.EntityFrameworkCore;

var options = new DbContextOptionsBuilder<ChinookContext>()
    // The copy next to the binary, read-only: works from any working directory and never creates an empty db.
    .UseSqlite($"Data Source={Path.Combine(AppContext.BaseDirectory, "chinook.db")};Mode=ReadOnly")
    .Options;

using var db = new ChinookContext(options);

Section("1. LEFT JOIN, the old way: GroupJoin + SelectMany + DefaultIfEmpty");
Console.WriteLine("""
    db.Artists
        .GroupJoin(db.Albums, artist => artist.Id, album => album.ArtistId,
            (artist, albums) => new { artist, albums })
        .SelectMany(x => x.albums.DefaultIfEmpty(),
            (x, album) => new ArtistAlbum { ..., AlbumTitle = album == null ? null : album.Title })
    """);
var oldLeft = JoinQueries.ArtistAlbumsOldWay(db);
var oldLeftSql = oldLeft.ToQueryString();
Console.WriteLine();
Console.WriteLine(oldLeftSql);
var oldLeftRows = oldLeft.ToList();
Console.WriteLine($"-> {oldLeftRows.Count} rows");

Section("2. LEFT JOIN, the EF Core 10 way: LeftJoin");
Console.WriteLine("""
    db.Artists
        .LeftJoin(db.Albums, artist => artist.Id, album => album.ArtistId,
            (artist, album) => new ArtistAlbum { ..., AlbumTitle = album == null ? null : album.Title })
    """);
var newLeft = JoinQueries.ArtistAlbumsLeftJoin(db);
var newLeftSql = newLeft.ToQueryString();
Console.WriteLine();
Console.WriteLine(newLeftSql);
var newLeftRows = newLeft.ToList();
Console.WriteLine($"-> {newLeftRows.Count} rows");
Console.WriteLine();
Console.WriteLine(newLeftSql == oldLeftSql
    ? "Same SQL, character for character."
    : "SQL differs (see above) - but both are a LEFT JOIN.");
Console.WriteLine(newLeftRows.SequenceEqual(oldLeftRows)
    ? "Same rows, same order."
    : "WARNING: the two queries returned different rows.");

var withoutAlbums = newLeftRows.Where(r => r.AlbumTitle is null).ToList();
Console.WriteLine();
Console.WriteLine($"{withoutAlbums.Count} artists have no albums; the LEFT JOIN keeps them with a null album. First five:");
foreach (var row in withoutAlbums.Take(5))
{
    Console.WriteLine($"  {row.ArtistId,4}  {row.ArtistName,-40} album: (null)");
}

Section("3. RIGHT JOIN: RightJoin keeps every row on the right-hand side");
Console.WriteLine("""
    db.Customers
        .RightJoin(db.Employees, customer => customer.SupportRepId, employee => (int?)employee.Id,
            (customer, employee) => new RepCustomer { ..., CustomerName = customer == null ? null : customer.FirstName + ... })
    """);
var right = JoinQueries.SupportRepsRightJoin(db);
Console.WriteLine();
Console.WriteLine(right.ToQueryString());
var rightRows = right.ToList();
Console.WriteLine($"-> {rightRows.Count} rows");
Console.WriteLine();
Console.WriteLine("Customers per employee (employees with 0 came through the RIGHT JOIN with a null customer):");
foreach (var group in rightRows.GroupBy(r => (r.EmployeeId, r.EmployeeName, r.EmployeeTitle)))
{
    var customers = group.Count(r => r.CustomerName is not null);
    Console.WriteLine($"  {group.Key.EmployeeName,-20} {group.Key.EmployeeTitle,-20} {customers,3} customers");
}

var oldRightRows = JoinQueries.SupportRepsOldWay(db).ToList();
Console.WriteLine();
Console.WriteLine("Before .NET 10 there was no RightJoin: you flipped the sides and wrote the GroupJoin/DefaultIfEmpty");
Console.WriteLine($"left join from Employees instead. It returns {oldRightRows.Count} rows - "
                  + (oldRightRows.SequenceEqual(rightRows) ? "the same rows." : "WARNING: different rows."));

Section("4. Side by side");
Console.WriteLine("""
    Old (any EF Core version)                     New (EF Core 10 / .NET 10)
    -------------------------------------------   -------------------------------------------
    outer.GroupJoin(inner, ok, ik,                outer.LeftJoin(inner, ok, ik,
        (o, inners) => new { o, inners })             (o, i) => new { o, i })
      .SelectMany(x => x.inners.DefaultIfEmpty(),
        (x, i) => new { x.o, i })

    (no RightJoin - swap the sides and            outer.RightJoin(inner, ok, ik,
     write the left join above)                        (o, i) => new { o, i })

    Same SQL either way. The win is readability: the intent is in the method name, not
    spread across three operators and a DefaultIfEmpty you have to recognize as a pattern.
    """);

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 80));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 80));
}
