extern alias hierarchyid;

using EFCoreDemos.Tests.Infrastructure;
using hierarchyid::hierarchyid.Org;
using Microsoft.EntityFrameworkCore;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// hierarchyid: Chinook's Employee.ReportsTo is copied into a hierarchyid column; IsDescendantOf, GetAncestor,
    /// GetLevel and GetReparentedValue answer tree questions in SQL, and a recursive CTE gives the same answers.
    /// Each SQL Server test class works in its own uniquely-named database (dropped on dispose); Chinook is only read.
    /// </summary>
    public class HierarchyIdTests : IDisposable
    {
        // Chinook: Andrew (1) runs the company and "reports to" himself; Nancy (2) has 3 sales agents (3, 4, 5);
        // Michael (6) has 2 IT staff (7, 8).
        private const int Andrew = 1, Nancy = 2, Michael = 6, Laura = 8;

        private readonly string _databaseName = $"HierarchyIdTest_{Guid.NewGuid():N}";
        private bool _created;

        private OrgContext CreateContext() =>
            new(OrgContext.CreateOptions(SqlServerTestServer.ConnectionString + $"Database={_databaseName};"));

        private async Task<OrgContext> CreateSeededContextAsync()
        {
            var source = await OrgChart.LoadChinookEmployeesAsync(SqlServerTestServer.ConnectionString + "Database=Chinook;");
            var db = CreateContext();
            _created = true;
            await OrgChart.ResetAsync(db, source);
            return db;
        }

        public void Dispose()
        {
            if (!_created) return;
            using var db = CreateContext();
            db.Database.EnsureDeleted();
        }

        private static readonly SourceEmployee[] ChinookShape =
        [
            new() { Id = 1, FirstName = "Andrew", LastName = "Adams", Title = "General Manager", ReportsTo = 1 },
            new() { Id = 2, FirstName = "Nancy", LastName = "Edwards", Title = "Sales Manager", ReportsTo = 1 },
            new() { Id = 3, FirstName = "Jane", LastName = "Peacock", Title = "Sales Support Agent", ReportsTo = 2 },
            new() { Id = 6, FirstName = "Michael", LastName = "Mitchell", Title = "IT Manager", ReportsTo = 1 },
            new() { Id = 7, FirstName = "Robert", LastName = "King", Title = "IT Staff", ReportsTo = 6 },
            new() { Id = 8, FirstName = "Laura", LastName = "Callahan", Title = "IT Staff", ReportsTo = 6 },
        ];

        [Fact]
        public void BuildOrgChart_TurnsReportsToIntoPaths_AndTreatsTheSelfReferenceAsTheRoot()
        {
            var paths = OrgChart.BuildOrgChart(ChinookShape).ToDictionary(e => e.Id);

            Assert.Equal("/", paths[Andrew].Path.ToString());
            Assert.Null(paths[Andrew].ManagerId);
            Assert.Equal("/1/", paths[Nancy].Path.ToString());
            Assert.Equal("/1/1/", paths[3].Path.ToString());
            Assert.Equal("/2/", paths[Michael].Path.ToString());
            Assert.Equal("/2/2/", paths[Laura].Path.ToString());
            Assert.Equal(Michael, paths[Laura].ManagerId);
            Assert.Equal(2, paths[Laura].Path.GetLevel());
        }

        [Fact]
        public void Queries_TranslateHierarchyIdMethodsToSql()
        {
            using var db = CreateContext();
            var manager = HierarchyId.Parse("/2/");

            Assert.Contains("[e].[Path].IsDescendantOf(@manager)", OrgChart.Descendants(db, manager).ToQueryString());
            Assert.Contains("[e].[Path].GetAncestor(1) = @manager", OrgChart.DirectReports(db, manager).ToQueryString());
            Assert.Contains("@employee.IsDescendantOf([e].[Path])", OrgChart.Ancestors(db, manager).ToQueryString());

            var tree = OrgChart.Tree(db).ToQueryString();
            Assert.Contains("[e].[Path].GetLevel()", tree);
            Assert.Contains("ORDER BY [e].[Path]", tree);
        }

        [SqlServerFact]
        public async Task SeededTree_IsDepthFirstAndAnswersTreeQuestions()
        {
            await using var db = await CreateSeededContextAsync();

            var tree = await OrgChart.Tree(db).ToListAsync();
            Assert.Equal(["/", "/1/", "/1/1/", "/1/2/", "/1/3/", "/2/", "/2/1/", "/2/2/"], tree.Select(r => r.Path));
            Assert.Equal([1, 2, 3, 4, 5, 6, 7, 8], tree.Select(r => r.Id));

            var root = await OrgChart.PathOfAsync(db, Andrew);
            Assert.Equal(7, await OrgChart.Descendants(db, root).CountAsync());
            Assert.Equal([Nancy, Michael], await OrgChart.DirectReports(db, root).Select(r => r.Id).ToListAsync());

            var chain = await OrgChart.Ancestors(db, await OrgChart.PathOfAsync(db, Laura)).ToListAsync();
            Assert.Equal([Andrew, Michael], chain.Select(r => r.Id));
            Assert.Equal([0, 1], chain.Select(r => (int)r.Level));
        }

        [SqlServerFact]
        public async Task RecursiveCte_ReturnsTheSameEmployeesAsIsDescendantOf()
        {
            await using var db = await CreateSeededContextAsync();

            foreach (var managerId in new[] { Andrew, Nancy, Michael, Laura })
            {
                var path = await OrgChart.PathOfAsync(db, managerId);
                var viaHierarchyId = await OrgChart.Descendants(db, path).Select(r => r.Id).ToListAsync();
                var viaCte = (await OrgChart.DescendantsCte(db, managerId).ToListAsync()).Select(e => e.Id);

                Assert.Equal(viaHierarchyId.Order(), viaCte.Order());
            }
        }

        [SqlServerFact]
        public async Task MoveSubtree_ReparentsTheWholeSubtreeInOneUpdate()
        {
            await using var db = await CreateSeededContextAsync();

            var (oldPath, newPath, moved) = await OrgChart.MoveSubtreeAsync(db, Michael, Nancy);

            Assert.Equal("/2/", oldPath.ToString());
            Assert.Equal("/1/4/", newPath.ToString()); // after Nancy's three agents
            Assert.Equal(3, moved);

            var paths = await db.Employees.AsNoTracking().ToDictionaryAsync(e => e.Id, e => e.Path.ToString());
            Assert.Equal("/1/4/", paths[Michael]);
            Assert.Equal("/1/4/1/", paths[7]);
            Assert.Equal("/1/4/2/", paths[Laura]);
            Assert.Equal("/1/1/", paths[3]); // untouched

            // Nancy now has 6 people under her, and the adjacency list agrees.
            var nancyPath = await OrgChart.PathOfAsync(db, Nancy);
            var underNancy = await OrgChart.Descendants(db, nancyPath).Select(r => r.Id).ToListAsync();
            Assert.Equal(6, underNancy.Count);
            var viaCte = (await OrgChart.DescendantsCte(db, Nancy).ToListAsync()).Select(e => e.Id);
            Assert.Equal(underNancy.Order(), viaCte.Order());
        }

        [SqlServerFact]
        public async Task MoveSubtree_UnderItsOwnDescendant_Throws()
        {
            await using var db = await CreateSeededContextAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => OrgChart.MoveSubtreeAsync(db, Michael, Laura));
        }

        [SqlServerFact]
        public async Task Reset_IsRepeatable()
        {
            await using var db = await CreateSeededContextAsync();
            await OrgChart.MoveSubtreeAsync(db, Michael, Nancy);

            var source = await OrgChart.LoadChinookEmployeesAsync(SqlServerTestServer.ConnectionString + "Database=Chinook;");
            await OrgChart.ResetAsync(db, source);

            var paths = await OrgChart.Tree(db).Select(r => r.Path).ToListAsync();
            Assert.Equal(["/", "/1/", "/1/1/", "/1/2/", "/1/3/", "/2/", "/2/1/", "/2/2/"], paths);
        }
    }
}
