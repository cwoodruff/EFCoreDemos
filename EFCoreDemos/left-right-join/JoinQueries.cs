using left_right_join.Chinook;

namespace left_right_join;

// Member-init (not positional) records: EF can only see through an object initializer when a later
// OrderBy refers to the projected members.
public sealed record ArtistAlbum
{
    public int ArtistId { get; init; }
    public string? ArtistName { get; init; }
    public string? AlbumTitle { get; init; }
}

public sealed record RepCustomer
{
    public int EmployeeId { get; init; }
    public string EmployeeName { get; init; } = "";
    public string? EmployeeTitle { get; init; }
    public string? CustomerName { get; init; }
}

/// <summary>
/// The same outer joins written the pre-EF 10 way and with the .NET 10 LeftJoin / RightJoin operators.
/// Each pair must produce the same SQL and the same rows.
/// </summary>
public static class JoinQueries
{
    /// <summary>Every artist with each of their albums, or a null album: the classic three-step pattern.</summary>
    public static IQueryable<ArtistAlbum> ArtistAlbumsOldWay(ChinookContext db) =>
        db.Artists
            .GroupJoin(db.Albums, artist => artist.Id, album => album.ArtistId,
                (artist, albums) => new { artist, albums })
            .SelectMany(x => x.albums.DefaultIfEmpty(),
                (x, album) => new ArtistAlbum
                {
                    ArtistId = x.artist.Id,
                    ArtistName = x.artist.Name,
                    AlbumTitle = album == null ? null : album.Title
                })
            .OrderBy(r => r.ArtistId)
            .ThenBy(r => r.AlbumTitle);

    /// <summary>The same left join with the .NET 10 operator: one call, says what it means.</summary>
    public static IQueryable<ArtistAlbum> ArtistAlbumsLeftJoin(ChinookContext db) =>
        db.Artists
            .LeftJoin(db.Albums, artist => artist.Id, album => album.ArtistId,
                (artist, album) => new ArtistAlbum
                {
                    ArtistId = artist.Id,
                    ArtistName = artist.Name,
                    AlbumTitle = album == null ? null : album.Title
                })
            .OrderBy(r => r.ArtistId)
            .ThenBy(r => r.AlbumTitle);

    /// <summary>
    /// Customers RIGHT JOIN Employees: every employee is kept, with each customer they support or a null.
    /// Before .NET 10 there was no RightJoin; you flipped it into a left join from the other side.
    /// </summary>
    public static IQueryable<RepCustomer> SupportRepsRightJoin(ChinookContext db) =>
        db.Customers
            .RightJoin(db.Employees, customer => customer.SupportRepId, employee => (int?)employee.Id,
                (customer, employee) => new RepCustomer
                {
                    EmployeeId = employee.Id,
                    EmployeeName = employee.FirstName + " " + employee.LastName,
                    EmployeeTitle = employee.Title,
                    CustomerName = customer == null ? null : customer.FirstName + " " + customer.LastName
                })
            .OrderBy(r => r.EmployeeId)
            .ThenBy(r => r.CustomerName);

    /// <summary>The pre-.NET 10 equivalent of the right join: swap the sides and write a left join.</summary>
    public static IQueryable<RepCustomer> SupportRepsOldWay(ChinookContext db) =>
        db.Employees
            .GroupJoin(db.Customers, employee => (int?)employee.Id, customer => customer.SupportRepId,
                (employee, customers) => new { employee, customers })
            .SelectMany(x => x.customers.DefaultIfEmpty(),
                (x, customer) => new RepCustomer
                {
                    EmployeeId = x.employee.Id,
                    EmployeeName = x.employee.FirstName + " " + x.employee.LastName,
                    EmployeeTitle = x.employee.Title,
                    CustomerName = customer == null ? null : customer.FirstName + " " + customer.LastName
                })
            .OrderBy(r => r.EmployeeId)
            .ThenBy(r => r.CustomerName);
}
