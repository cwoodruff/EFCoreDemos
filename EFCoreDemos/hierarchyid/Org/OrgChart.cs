using Microsoft.EntityFrameworkCore;

namespace hierarchyid.Org;

/// <summary>A row of Chinook's Employee table (Chinook on SQL Server names the key column Id).</summary>
public sealed class SourceEmployee
{
    public int Id { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Title { get; init; } = "";
    public int? ReportsTo { get; init; }
}

/// <summary>A node of the org chart as the queries return it.</summary>
public sealed record OrgRow
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Title { get; init; } = "";
    public short Level { get; init; }
    public string Path { get; init; } = "";
}

/// <summary>Seeding, the hierarchyid queries, the recursive-CTE equivalent and the subtree move.</summary>
public static class OrgChart
{
    public const string ServerConnectionString =
        "Server=localhost,1433;User Id=sa;Password=8riwudeg!!;TrustServerCertificate=True;Application Name=EFCoreDemos-HierarchyId;";

    public const string ChinookConnectionString = ServerConnectionString + "Database=Chinook;";
    public const string DemoConnectionString = ServerConnectionString + "Database=HierarchyDemo;";

    /// <summary>Reads Chinook's employees (read-only; the shared Chinook schema is not touched).</summary>
    public static async Task<List<SourceEmployee>> LoadChinookEmployeesAsync(string chinookConnectionString)
    {
        await using var chinook = new DbContext(new DbContextOptionsBuilder().UseSqlServer(chinookConnectionString).Options);
        return await chinook.Database
            .SqlQuery<SourceEmployee>($"SELECT Id, FirstName, LastName, Title, ReportsTo FROM dbo.Employee")
            .OrderBy(e => e.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Turns the adjacency list (ReportsTo) into hierarchyid paths: the root is "/", and each manager's
    /// reports get the next child slot in id order ("/1/", "/2/", ...). Chinook's general manager reports
    /// to himself (ReportsTo = own Id); that is treated as "no manager".
    /// </summary>
    public static List<Employee> BuildOrgChart(IEnumerable<SourceEmployee> source)
    {
        var people = source.ToList();
        int? ManagerOf(SourceEmployee e) => e.ReportsTo == e.Id ? null : e.ReportsTo;

        var result = new List<Employee>();
        void Add(SourceEmployee person, HierarchyId path)
        {
            result.Add(new Employee
            {
                Id = person.Id,
                Name = $"{person.FirstName} {person.LastName}",
                Title = person.Title,
                ManagerId = ManagerOf(person),
                Path = path
            });

            HierarchyId? previous = null;
            foreach (var report in people.Where(p => ManagerOf(p) == person.Id).OrderBy(p => p.Id))
            {
                // GetDescendant(previous, null) = the next free child slot after the previous sibling.
                var child = path.GetDescendant(previous, null);
                Add(report, child);
                previous = child;
            }
        }

        foreach (var root in people.Where(p => ManagerOf(p) is null).OrderBy(p => p.Id))
        {
            Add(root, HierarchyId.GetRoot());
        }

        return result;
    }

    /// <summary>Creates the demo database if needed and replaces its contents with a fresh org chart.</summary>
    public static async Task ResetAsync(OrgContext db, IEnumerable<SourceEmployee> source)
    {
        await db.Database.EnsureCreatedAsync();
        await db.Employees.ExecuteDeleteAsync();
        db.Employees.AddRange(BuildOrgChart(source));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    public static Task<HierarchyId> PathOfAsync(OrgContext db, int employeeId) =>
        db.Employees.Where(e => e.Id == employeeId).Select(e => e.Path).SingleAsync();

    private static IQueryable<OrgRow> ToRows(IQueryable<Employee> employees) =>
        employees
            .OrderBy(e => e.Path)
            .Select(e => new OrgRow
            {
                Id = e.Id,
                Name = e.Name,
                Title = e.Title,
                Level = e.Path.GetLevel(),
                Path = e.Path.ToString()
            });

    /// <summary>The whole tree, depth-first (that is simply ORDER BY Path).</summary>
    public static IQueryable<OrgRow> Tree(OrgContext db) => ToRows(db.Employees);

    /// <summary>Everyone under a manager, at any depth. IsDescendantOf includes the node itself, so exclude it.</summary>
    public static IQueryable<OrgRow> Descendants(OrgContext db, HierarchyId manager) =>
        ToRows(db.Employees.Where(e => e.Path.IsDescendantOf(manager) && e.Path != manager));

    /// <summary>Only the people one level down.</summary>
    public static IQueryable<OrgRow> DirectReports(OrgContext db, HierarchyId manager) =>
        ToRows(db.Employees.Where(e => e.Path.GetAncestor(1) == manager));

    /// <summary>The chain of command above someone: every node they are a descendant of, root first.</summary>
    public static IQueryable<OrgRow> Ancestors(OrgContext db, HierarchyId employee) =>
        ToRows(db.Employees.Where(e => employee.IsDescendantOf(e.Path) && e.Path != employee));

    /// <summary>
    /// The same question as <see cref="Descendants"/>, answered the pre-hierarchyid way: a recursive CTE
    /// over the ManagerId adjacency list. SQL Server doesn't allow a WITH inside a subquery, so EF can't
    /// compose over this - filtering and ordering live in the SQL itself.
    /// </summary>
    public static IQueryable<Employee> DescendantsCte(OrgContext db, int managerId) =>
        db.Employees.FromSql($"""
            WITH Subordinates AS (
                SELECT Id FROM Employees WHERE ManagerId = {managerId}
                UNION ALL
                SELECT e.Id FROM Employees AS e
                INNER JOIN Subordinates AS s ON e.ManagerId = s.Id
            )
            SELECT e.* FROM Employees AS e
            INNER JOIN Subordinates AS s ON s.Id = e.Id
            """);

    /// <summary>
    /// Moves an employee and everyone under them to a new manager with one UPDATE: the subtree root takes
    /// the new manager's next free child slot, and GetReparentedValue rewrites the prefix of every path below.
    /// Returns the number of rows whose path changed.
    /// </summary>
    public static async Task<(HierarchyId OldPath, HierarchyId NewPath, int RowsMoved)> MoveSubtreeAsync(
        OrgContext db, int employeeId, int newManagerId)
    {
        var oldPath = await PathOfAsync(db, employeeId);
        var managerPath = await PathOfAsync(db, newManagerId);

        if (managerPath.IsDescendantOf(oldPath))
            throw new InvalidOperationException("Can't move a subtree underneath itself.");

        var lastChild = await db.Employees
            .Where(e => e.Path.GetAncestor(1) == managerPath)
            .OrderByDescending(e => e.Path)
            .Select(e => e.Path)
            .FirstOrDefaultAsync();
        var newPath = managerPath.GetDescendant(lastChild, null);

        var moved = await db.Employees
            .Where(e => e.Path.IsDescendantOf(oldPath))
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Path, e => e.Path.GetReparentedValue(oldPath, newPath)));

        // Keep the adjacency list in step so the CTE sees the same tree.
        await db.Employees
            .Where(e => e.Id == employeeId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.ManagerId, newManagerId));

        return (oldPath, newPath, moved);
    }
}
