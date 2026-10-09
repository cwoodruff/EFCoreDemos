using System.Diagnostics;
using System.Reflection;
using compiled_models.Chinook;
using compiled_models.CompiledModels;
using Microsoft.EntityFrameworkCore;

// The compiled model in ./CompiledModels was generated with:
//   dotnet ef dbcontext optimize --output-dir CompiledModels --namespace compiled_models.CompiledModels
// Re-run that command whenever the model (entities / OnModelCreating) changes.
//
// EF Core discovers a compiled model automatically through the [assembly: DbContextModel] attribute,
// but we call UseModel(...) explicitly so the demo is unambiguous.
// For the "without" case we use RuntimeModelChinookContext, a subclass the compiled model is NOT
// registered for, so EF has to run OnModelCreating and build the model at runtime.
//
// Model building is a once-per-process startup cost, so each variant is measured in a fresh
// child process (otherwise whichever runs first also pays JIT / EF service-provider warm-up).
public class Program
{
    private const int Runs = 5;

    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] is "--compiled" or "--runtime")
        {
            RunOnce(compiled: args[0] == "--compiled");
            return;
        }

        Console.WriteLine($"Cold start, {Runs} fresh processes per variant (alternating)...");
        var compiled = new List<(double Model, double FirstQuery)>();
        var runtime = new List<(double Model, double FirstQuery)>();
        for (var i = 0; i < Runs; i++)
        {
            runtime.Add(RunChild("--runtime"));
            compiled.Add(RunChild("--compiled"));
        }

        Report("Runtime-built model (OnModelCreating)", runtime);
        Report("Compiled model (UseModel)", compiled);
        Console.WriteLine();
        Console.WriteLine("Model build cost is paid once per process (models are cached per context type),");
        Console.WriteLine("so compiled models improve startup time, not steady-state query time.");
        Console.WriteLine("The gain grows with model size; Chinook has only 11 entity types.");
    }

    private static void RunOnce(bool compiled)
    {
        var sw = Stopwatch.StartNew();
        using ChinookContext context = compiled
            ? new ChinookContext(new DbContextOptionsBuilder<ChinookContext>()
                .UseSqlite("Data Source=chinook.db")
                .UseModel(ChinookContextModel.Instance)
                .Options)
            : new RuntimeModelChinookContext(new DbContextOptionsBuilder<ChinookContext>()
                .UseSqlite("Data Source=chinook.db")
                .Options);

        var model = context.Model; // forces the model to be loaded (compiled) or built (runtime)
        var modelMs = sw.Elapsed.TotalMilliseconds;
        var artist = context.Artists.AsNoTracking().OrderBy(a => a.Id).First();
        var firstQueryMs = sw.Elapsed.TotalMilliseconds;

        // Machine-readable line for the parent process.
        Console.WriteLine(FormattableString.Invariant($"RESULT {modelMs} {firstQueryMs} {model.GetType().Name} {artist.Name}"));
    }

    private static (double Model, double FirstQuery) RunChild(string mode)
    {
        var processPath = Environment.ProcessPath!;
        var viaDotnetHost = Path.GetFileNameWithoutExtension(processPath) == "dotnet";
        var psi = new ProcessStartInfo(processPath)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = Environment.CurrentDirectory
        };
        if (viaDotnetHost) psi.ArgumentList.Add(Assembly.GetEntryAssembly()!.Location);
        psi.ArgumentList.Add(mode);

        using var process = Process.Start(psi)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var line = output.Split('\n').First(l => l.StartsWith("RESULT "));
        var parts = line.Split(' ');
        return (double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static void Report(string label, List<(double Model, double FirstQuery)> results)
    {
        Console.WriteLine($"--- {label} ---");
        Console.WriteLine($"  Context + model ready:  median {Median(results.Select(r => r.Model)),7:F1} ms");
        Console.WriteLine($"  ...plus first query:    median {Median(results.Select(r => r.FirstQuery)),7:F1} ms");
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        return sorted[sorted.Length / 2];
    }
}

// Same model, different CLR type: the compiled model is registered only for ChinookContext,
// so this context builds its model from OnModelCreating at runtime.
public class RuntimeModelChinookContext(DbContextOptions<ChinookContext> options) : ChinookContext(options);
