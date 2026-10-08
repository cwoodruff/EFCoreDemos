using BenchmarkDotNet.Running;
using ef_core_sprocs_perf.Benchmarks;

// Run in Release:  dotnet run -c Release
BenchmarkSwitcher.FromAssembly(typeof(AlbumQueryBenchmarks).Assembly).Run(args);
