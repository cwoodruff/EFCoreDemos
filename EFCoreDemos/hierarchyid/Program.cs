using hierarchyid.Org;
using Microsoft.EntityFrameworkCore;

// HierarchyId: store a tree as paths ("/", "/1/", "/1/2/") and ask tree questions with plain LINQ.
// The org chart is copied from Chinook's Employee.ReportsTo into a demo-owned HierarchyDemo database.

var logCommands = false;
var options = OrgContext.CreateOptions(OrgChart.DemoConnectionString, message =>
{
    if (logCommands) Console.WriteLine(message);
});

await using var db = new OrgContext(options);

var source = await OrgChart.LoadChinookEmployeesAsync(OrgChart.ChinookConnectionString);
await OrgChart.ResetAsync(db, source);

const int andrew = 1, nancy = 2, michael = 6, laura = 8;

Section("1. Chinook's adjacency list -> hierarchyid paths");
Console.WriteLine("Chinook stores the tree as Employee.ReportsTo (a parent id per row):");
foreach (var e in source)
{
    var note = e.ReportsTo == e.Id ? "   <- reports to himself: treated as the root" : "";
    Console.WriteLine($"  {e.Id}  {e.FirstName + " " + e.LastName,-18} ReportsTo = {e.ReportsTo?.ToString() ?? "NULL"}{note}");
}

Console.WriteLine();
Console.WriteLine("HierarchyDemo.Employees stores the whole path instead (built with HierarchyId.GetDescendant):");
var treeQuery = OrgChart.Tree(db);
Console.WriteLine(treeQuery.ToQueryString());
Console.WriteLine();
PrintTree(await treeQuery.ToListAsync());
Console.WriteLine();
Console.WriteLine("ORDER BY Path is a depth-first walk: every manager comes right before their team.");

Section("2. Tree questions as LINQ");
var andrewPath = await OrgChart.PathOfAsync(db, andrew);
var lauraPath = await OrgChart.PathOfAsync(db, laura);

var everyoneUnderAndrew = OrgChart.Descendants(db, andrewPath);
Console.WriteLine("-- Everyone under Andrew, any depth: Path.IsDescendantOf(@manager) --");
Console.WriteLine(everyoneUnderAndrew.ToQueryString());
PrintRows(await everyoneUnderAndrew.ToListAsync());

var andrewsReports = OrgChart.DirectReports(db, andrewPath);
Console.WriteLine("-- Andrew's direct reports only: Path.GetAncestor(1) == @manager --");
Console.WriteLine(andrewsReports.ToQueryString());
PrintRows(await andrewsReports.ToListAsync());

var lauraChain = OrgChart.Ancestors(db, lauraPath);
Console.WriteLine($"-- Laura Callahan's chain of command ({lauraPath}): @employee.IsDescendantOf(Path) --");
Console.WriteLine(lauraChain.ToQueryString());
PrintRows(await lauraChain.ToListAsync());

var levels = await db.Employees
    .GroupBy(e => e.Path.GetLevel())
    .Select(g => new { Level = g.Key, Count = g.Count() })
    .OrderBy(x => x.Level)
    .ToListAsync();
Console.WriteLine("-- Head count per level: GROUP BY Path.GetLevel() --");
foreach (var level in levels)
{
    Console.WriteLine($"  level {level.Level}: {level.Count}");
}

Section("3. The same question without hierarchyid: a recursive CTE via FromSql");
var cteQuery = OrgChart.DescendantsCte(db, andrew);
// ToQueryString drops the @ from FromSql's interpolated parameter names in the DECLARE line.
Console.WriteLine(cteQuery.ToQueryString().Replace("DECLARE p0 ", "DECLARE @p0 "));
var cteRows = await cteQuery.ToListAsync();
var cteIds = cteRows.Select(e => e.Id).Order().ToList();
var hierarchyIds = (await everyoneUnderAndrew.ToListAsync()).Select(r => r.Id).Order().ToList();
Console.WriteLine($"-> {cteRows.Count} rows; same employees as IsDescendantOf: {(cteIds.SequenceEqual(hierarchyIds) ? "yes" : "NO")}");
Console.WriteLine();
Console.WriteLine("The CTE re-walks the tree one level per iteration on every query, and it can't be composed:");
Console.WriteLine("SQL Server won't take a WITH inside a subquery, so Where/OrderBy have to be written into the SQL.");
Console.WriteLine("It also loops on Chinook's raw data - Andrew's ReportsTo = 1 points at himself, so the recursion");
Console.WriteLine("never ends (error 530, MAXRECURSION 100). IsDescendantOf is a range seek on the Path index.");

Section("4. Move a subtree: Michael Mitchell's IT team now reports to Nancy Edwards");
await using (var transaction = await db.Database.BeginTransactionAsync())
{
    logCommands = true;
    var (oldPath, newPath, moved) = await OrgChart.MoveSubtreeAsync(db, michael, nancy);
    logCommands = false;

    Console.WriteLine();
    Console.WriteLine($"Michael {oldPath} -> {newPath}: {moved} rows rewritten by one UPDATE with GetReparentedValue.");
    Console.WriteLine();
    PrintTree(await OrgChart.Tree(db).ToListAsync());
    Console.WriteLine();

    var underNancy = await OrgChart.Descendants(db, await OrgChart.PathOfAsync(db, nancy)).ToListAsync();
    Console.WriteLine($"Everyone under Nancy now: {underNancy.Count} (her 3 agents + Michael + his 2 staff, one level deeper).");

    await transaction.RollbackAsync();
}

Console.WriteLine("Rolled back, so the next run starts from the same tree.");
Console.WriteLine();

static void Section(string title)
{
    Console.WriteLine();
    Console.WriteLine(new string('=', 78));
    Console.WriteLine(title);
    Console.WriteLine(new string('=', 78));
}

static void PrintTree(IEnumerable<OrgRow> rows)
{
    foreach (var row in rows)
    {
        var indent = new string(' ', row.Level * 4);
        Console.WriteLine($"  {row.Path,-10} {indent}{row.Name} ({row.Title})");
    }
}

static void PrintRows(IReadOnlyList<OrgRow> rows)
{
    foreach (var row in rows)
    {
        Console.WriteLine($"  level {row.Level}  {row.Path,-10} {row.Name} ({row.Title})");
    }

    Console.WriteLine($"-> {rows.Count} rows");
    Console.WriteLine();
}
