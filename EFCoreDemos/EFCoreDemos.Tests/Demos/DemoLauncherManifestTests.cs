using System.Text.Json;
using System.Text.Json.Serialization;

namespace EFCoreDemos.Tests.Demos
{
    /// <summary>
    /// DemoLauncher.Web: demos.json drives the launcher UI and `dotnet run --project {projectPath}`.
    /// A typo there only shows up live on stage, so validate the manifest against the repo.
    /// </summary>
    public class DemoLauncherManifestTests
    {
        // Mirrors DemoLauncher.Web.Manifest / Demo (the launcher project is not referenced by the tests).
        private sealed class Manifest
        {
            [JsonPropertyName("demos")]
            public List<Demo>? Demos { get; set; }
        }

        private sealed class Demo
        {
            public string? Id { get; set; }
            public int Insight { get; set; }
            public string? Block { get; set; }
            public string? Title { get; set; }
            public string? Blurb { get; set; }
            public string? ProjectPath { get; set; }
            public bool SupportsBenchmark { get; set; }
            public string? SpecialCase { get; set; }
            public int? WebServerPort { get; set; }
            public List<string>? WebServerEndpoints { get; set; }
            public Info? Info { get; set; }
        }

        private sealed class Info
        {
            public string? Summary { get; set; }
            public List<string>? Highlights { get; set; }
            public List<string>? KeyApis { get; set; }
            public string? Takeaway { get; set; }
            public string? Database { get; set; }
            public string? Setup { get; set; }
            public List<LearnLink>? LearnLinks { get; set; }
        }

        private sealed class LearnLink
        {
            public string? Title { get; set; }
            public string? Url { get; set; }
        }

        private static string RepoRoot()
        {
            // Same lookup the launcher uses (ResolveDemoRoot): walk up to the folder containing EFCoreDemos.sln.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "EFCoreDemos.sln"))) return dir.FullName;
                dir = dir.Parent;
            }

            throw new InvalidOperationException("Could not locate EFCoreDemos.sln above " + AppContext.BaseDirectory);
        }

        private static string LauncherDir() => Path.Combine(RepoRoot(), "DemoLauncher.Web");

        private static List<Demo> LoadDemos()
        {
            var json = File.ReadAllText(Path.Combine(LauncherDir(), "demos.json"));
            var manifest = JsonSerializer.Deserialize<Manifest>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            Assert.NotNull(manifest);
            Assert.NotNull(manifest.Demos);
            return manifest.Demos;
        }

        [Fact]
        public void Manifest_ParsesAndHasDemos()
        {
            Assert.NotEmpty(LoadDemos());
        }

        [Fact]
        public void EveryDemo_HasRequiredFields()
        {
            foreach (var demo in LoadDemos())
            {
                var label = demo.Id ?? demo.Title ?? "(unnamed)";
                Assert.False(string.IsNullOrWhiteSpace(demo.Id), $"{label}: id is missing");
                Assert.False(string.IsNullOrWhiteSpace(demo.Title), $"{label}: title is missing");
                Assert.False(string.IsNullOrWhiteSpace(demo.Blurb), $"{label}: blurb is missing");
                Assert.False(string.IsNullOrWhiteSpace(demo.Block), $"{label}: block is missing");
                Assert.False(string.IsNullOrWhiteSpace(demo.ProjectPath), $"{label}: projectPath is missing");
                Assert.True(demo.Insight > 0, $"{label}: insight must be a positive number");
            }
        }

        [Fact]
        public void DemoIds_AreUnique()
        {
            var duplicates = LoadDemos()
                .GroupBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            Assert.Empty(duplicates);
        }

        [Fact]
        public void EveryProjectPath_PointsAtAnExistingCsproj()
        {
            var root = RepoRoot();
            var missing = LoadDemos()
                .Where(d => !string.IsNullOrWhiteSpace(d.ProjectPath))
                .Where(d => !d.ProjectPath!.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
                            || !File.Exists(Path.Combine(root, d.ProjectPath)))
                .Select(d => $"{d.Id}: {d.ProjectPath}")
                .ToList();

            Assert.Empty(missing);
        }

        [Fact]
        public void BenchmarkDemos_HaveARecordedBenchmarkFile()
        {
            // Benchmark mode replays DemoLauncher.Web/benchmarks/{id}.txt instead of running the project.
            var benchmarksDir = Path.Combine(LauncherDir(), "benchmarks");
            var missing = LoadDemos()
                .Where(d => d.SupportsBenchmark)
                .Where(d => !File.Exists(Path.Combine(benchmarksDir, d.Id + ".txt")))
                .Select(d => d.Id)
                .ToList();

            Assert.Empty(missing);
        }

        [Fact]
        public void EveryDemo_HasTheSameInfoSections()
        {
            // The launcher's (i) panel shows the same sections for every demo, so every demo must fill them all.
            foreach (var demo in LoadDemos())
            {
                var info = demo.Info;
                Assert.True(info != null, $"{demo.Id}: info is missing");
                Assert.False(string.IsNullOrWhiteSpace(info.Summary), $"{demo.Id}: info.summary is missing");
                Assert.False(string.IsNullOrWhiteSpace(info.Takeaway), $"{demo.Id}: info.takeaway is missing");
                Assert.False(string.IsNullOrWhiteSpace(info.Database), $"{demo.Id}: info.database is missing");
                Assert.False(string.IsNullOrWhiteSpace(info.Setup), $"{demo.Id}: info.setup is missing (use \"None\")");
                Assert.True(info.Highlights is { Count: >= 3 }, $"{demo.Id}: info.highlights needs at least 3 items");
                Assert.All(info.Highlights, h => Assert.False(string.IsNullOrWhiteSpace(h), $"{demo.Id}: empty highlight"));
                Assert.True(info.KeyApis is { Count: >= 1 }, $"{demo.Id}: info.keyApis is empty");
                Assert.All(info.KeyApis, a => Assert.False(string.IsNullOrWhiteSpace(a), $"{demo.Id}: empty key API"));
                Assert.True(info.LearnLinks is { Count: >= 1 }, $"{demo.Id}: info.learnLinks is empty");
                Assert.All(info.LearnLinks, l =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(l.Title), $"{demo.Id}: learn link without a title");
                    Assert.True(l.Url != null && l.Url.StartsWith("https://learn.microsoft.com/", StringComparison.Ordinal),
                        $"{demo.Id}: learn link '{l.Url}' is not a Microsoft Learn URL");
                });
            }
        }

        [Fact]
        public void WebServerCurlDemos_DeclarePortAndEndpoints()
        {
            foreach (var demo in LoadDemos().Where(d => d.SpecialCase == "webserver-curl"))
            {
                Assert.True(demo.WebServerPort is > 0 and <= 65535, $"{demo.Id}: webServerPort is missing or invalid");
                Assert.NotNull(demo.WebServerEndpoints);
                Assert.NotEmpty(demo.WebServerEndpoints);
                Assert.All(demo.WebServerEndpoints, e => Assert.StartsWith("/", e));
            }
        }
    }
}
