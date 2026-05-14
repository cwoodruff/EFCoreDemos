using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using DemoLauncher.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

var demoRoot = ResolveDemoRoot();
var manifestPath = Path.Combine(AppContext.BaseDirectory, "demos.json");
var benchmarksDir = Path.Combine(AppContext.BaseDirectory, "benchmarks");

var manifest = LoadManifest(manifestPath);
var runner = new DemoRunner(demoRoot, benchmarksDir);

app.MapGet("/api/demos", () => Results.Json(new
{
    demos = manifest.Demos,
    demoRoot
}));

app.MapGet("/api/run/{id}", async (string id, string? mode, HttpContext ctx, CancellationToken ct) =>
{
    var demo = manifest.Demos.FirstOrDefault(d => d.Id == id);
    if (demo is null) return Results.NotFound(new { error = $"Unknown demo '{id}'." });

    var runMode = (mode ?? "run").ToLowerInvariant();
    if (runMode == "benchmark" && !demo.SupportsBenchmark)
    {
        return Results.BadRequest(new { error = $"Demo '{id}' does not support benchmark mode." });
    }

    ctx.Response.Headers.Append("Content-Type", "text/event-stream");
    ctx.Response.Headers.Append("Cache-Control", "no-cache");
    ctx.Response.Headers.Append("X-Accel-Buffering", "no");

    await runner.StreamAsync(demo, runMode, ctx.Response, ct);
    return Results.Empty;
});

app.MapPost("/api/stop", () =>
{
    var stopped = runner.Stop();
    return Results.Json(new { stopped });
});

app.MapGet("/api/health", () => Results.Json(new { ok = true, demoRoot, demoCount = manifest.Demos.Count }));

app.Run();
return;

static string ResolveDemoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        var sln = Path.Combine(dir.FullName, "EFCoreDemos.sln");
        if (File.Exists(sln)) return dir.FullName;
        dir = dir.Parent;
    }
    throw new InvalidOperationException("Could not locate EFCoreDemos.sln by walking up from the launcher binary.");
}

static Manifest LoadManifest(string path)
{
    var json = File.ReadAllText(path);
    var manifest = JsonSerializer.Deserialize<Manifest>(json, new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    }) ?? throw new InvalidOperationException("demos.json deserialized to null.");
    return manifest;
}

namespace DemoLauncher.Web
{
    public sealed class Manifest
    {
        [JsonPropertyName("demos")]
        public List<Demo> Demos { get; set; } = new();
    }

    public sealed class Demo
    {
        public string Id { get; set; } = "";
        public int Insight { get; set; }
        public string Block { get; set; } = "";
        public string Title { get; set; } = "";
        public string Blurb { get; set; } = "";
        public string ProjectPath { get; set; } = "";
        public bool SupportsBenchmark { get; set; }
        public string? SpecialCase { get; set; }
        public int? WebServerPort { get; set; }
        public List<string>? WebServerEndpoints { get; set; }
    }

    public sealed class DemoRunner
    {
        private readonly string _demoRoot;
        private readonly string _benchmarksDir;
        private readonly object _lock = new();
        private Process? _current;

        public DemoRunner(string demoRoot, string benchmarksDir)
        {
            _demoRoot = demoRoot;
            _benchmarksDir = benchmarksDir;
        }

        public bool Stop()
        {
            lock (_lock)
            {
                if (_current is null || _current.HasExited) return false;
                try { _current.Kill(entireProcessTree: true); } catch { }
                _current = null;
                return true;
            }
        }

        public async Task StreamAsync(Demo demo, string mode, HttpResponse response, CancellationToken ct)
        {
            // Cancel any in-flight demo before starting a new one.
            Stop();

            // Pre-recorded benchmark replay - no dotnet run.
            if (mode == "benchmark")
            {
                var file = Path.Combine(_benchmarksDir, demo.Id + ".txt");
                if (!File.Exists(file))
                {
                    await WriteEventAsync(response, "stderr", $"[no recorded benchmark for '{demo.Id}' at {file}]", ct);
                    await WriteEventAsync(response, "exit", "1", ct);
                    return;
                }

                await WriteEventAsync(response, "info", $"replaying benchmarks/{demo.Id}.txt", ct);
                foreach (var line in await File.ReadAllLinesAsync(file, ct))
                {
                    await WriteEventAsync(response, "stdout", line, ct);
                    await Task.Delay(20, ct);
                }
                await WriteEventAsync(response, "exit", "0", ct);
                return;
            }

            // dbcontext-lifetimes-di runs as a web server - launch it, then curl its endpoints.
            if (demo.SpecialCase == "webserver-curl")
            {
                await RunWebServerCurlAsync(demo, response, ct);
                return;
            }

            await RunDotnetAsync(demo, mode, response, ct);
        }

        private async Task RunDotnetAsync(Demo demo, string mode, HttpResponse response, CancellationToken ct)
        {
            var projectFile = Path.Combine(_demoRoot, demo.ProjectPath);
            var projectDir = Path.GetDirectoryName(projectFile)!;

            var args = new List<string> { "run", "--project", projectFile, "-c", "Release" };
            // (Live-benchmark mode would append "--", "benchmark" here. We use replay instead.)

            var psi = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = projectDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment = { ["DOTNET_NOLOGO"] = "1" }
            };
            foreach (var a in args) psi.ArgumentList.Add(a);

            await WriteEventAsync(response, "info", $"$ dotnet {string.Join(' ', args)}", ct);
            await WriteEventAsync(response, "info", $"  (cwd: {projectDir})", ct);

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            lock (_lock) { _current = process; }

            var channel = Channel.CreateUnbounded<(string Kind, string Line)>();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null) channel.Writer.TryWrite(("stdout", e.Data));
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null) channel.Writer.TryWrite(("stderr", e.Data));
            };
            process.Exited += (_, _) => channel.Writer.TryComplete();

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                await WriteEventAsync(response, "stderr", $"failed to start dotnet: {ex.Message}", ct);
                await WriteEventAsync(response, "exit", "-1", ct);
                return;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await foreach (var (kind, line) in channel.Reader.ReadAllAsync(ct))
                {
                    await WriteEventAsync(response, kind, line, ct);
                }
                await process.WaitForExitAsync(ct);
                await WriteEventAsync(response, "exit", process.ExitCode.ToString(), ct);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                await WriteEventAsync(response, "info", "[cancelled]", CancellationToken.None);
                await WriteEventAsync(response, "exit", "-1", CancellationToken.None);
            }
            finally
            {
                lock (_lock) { if (_current == process) _current = null; }
            }
        }

        private async Task RunWebServerCurlAsync(Demo demo, HttpResponse response, CancellationToken ct)
        {
            var projectFile = Path.Combine(_demoRoot, demo.ProjectPath);
            var projectDir = Path.GetDirectoryName(projectFile)!;
            var port = demo.WebServerPort ?? 5193;
            var url = $"http://localhost:{port}";

            var psi = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = projectDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                Environment =
                {
                    ["DOTNET_NOLOGO"] = "1",
                    ["ASPNETCORE_URLS"] = url,
                    ["ASPNETCORE_ENVIRONMENT"] = "Development"
                }
            };
            psi.ArgumentList.Add("run");
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add(projectFile);
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("Release");

            await WriteEventAsync(response, "info", $"$ dotnet run --project {demo.ProjectPath} -c Release", ct);
            await WriteEventAsync(response, "info", $"  (web server on {url})", ct);

            var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            lock (_lock) { _current = process; }

            try
            {
                process.Start();
                _ = ForwardAsync(process.StandardError, response, "stderr", ct);
                _ = ForwardAsync(process.StandardOutput, response, "stdout", ct);

                // Wait for "Now listening on" or similar.
                var ready = await WaitForUrlAsync(url, TimeSpan.FromSeconds(20), ct);
                if (!ready)
                {
                    await WriteEventAsync(response, "stderr", "[server did not become ready in 20s]", ct);
                    try { process.Kill(entireProcessTree: true); } catch { }
                    await WriteEventAsync(response, "exit", "-1", ct);
                    return;
                }

                await WriteEventAsync(response, "info", "", ct);
                await WriteEventAsync(response, "info", "=== Calling endpoints in sequence ===", ct);

                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var endpoints = demo.WebServerEndpoints ?? new List<string>();
                var callIndex = new Dictionary<string, int>();

                foreach (var ep in endpoints)
                {
                    callIndex[ep] = callIndex.GetValueOrDefault(ep) + 1;
                    var callTag = callIndex[ep] > 1 ? $"  (call #{callIndex[ep]})" : "";
                    await WriteEventAsync(response, "info", "", ct);
                    await WriteEventAsync(response, "info", $"GET {url}{ep}{callTag}", ct);

                    try
                    {
                        using var resp = await http.GetAsync(url + ep, ct);
                        await WriteEventAsync(response, "stdout", $"  HTTP {(int)resp.StatusCode} {resp.StatusCode}", ct);
                        var body = await resp.Content.ReadAsStringAsync(ct);
                        if (!string.IsNullOrWhiteSpace(body))
                        {
                            foreach (var line in body.Split('\n'))
                            {
                                await WriteEventAsync(response, "stdout", "  " + line.TrimEnd('\r'), ct);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        await WriteEventAsync(response, "stderr", $"  call failed: {ex.Message}", ct);
                    }

                    await Task.Delay(200, ct);
                }

                await WriteEventAsync(response, "info", "", ct);
                await WriteEventAsync(response, "info", "=== Done. Shutting down server. ===", ct);
                try { process.Kill(entireProcessTree: true); } catch { }
                await WriteEventAsync(response, "exit", "0", ct);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                await WriteEventAsync(response, "info", "[cancelled]", CancellationToken.None);
                await WriteEventAsync(response, "exit", "-1", CancellationToken.None);
            }
            finally
            {
                lock (_lock) { if (_current == process) _current = null; }
            }
        }

        private static async Task ForwardAsync(StreamReader reader, HttpResponse response, string kind, CancellationToken ct)
        {
            try
            {
                string? line;
                while ((line = await reader.ReadLineAsync(ct)) is not null)
                {
                    await WriteEventAsync(response, kind, line, ct);
                }
            }
            catch { }
        }

        private static async Task<bool> WaitForUrlAsync(string baseUrl, TimeSpan timeout, CancellationToken ct)
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var resp = await http.GetAsync(baseUrl + "/", ct);
                    if ((int)resp.StatusCode < 500) return true;
                }
                catch { /* server not up yet */ }
                await Task.Delay(250, ct);
            }
            return false;
        }

        private static readonly SemaphoreSlim WriteLock = new(1, 1);

        private static async Task WriteEventAsync(HttpResponse response, string kind, string data, CancellationToken ct)
        {
            await WriteLock.WaitAsync(ct);
            try
            {
                var safe = data.Replace("\r", string.Empty);
                var sb = new StringBuilder();
                sb.Append("event: ").Append(kind).Append('\n');
                foreach (var line in safe.Split('\n'))
                {
                    sb.Append("data: ").Append(line).Append('\n');
                }
                sb.Append('\n');
                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                await response.Body.WriteAsync(bytes, ct);
                await response.Body.FlushAsync(ct);
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            finally
            {
                WriteLock.Release();
            }
        }
    }
}
