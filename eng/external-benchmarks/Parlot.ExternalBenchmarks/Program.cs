using System.Reflection;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace Parlot.ExternalBenchmarks;

public static class Program
{
    public static int Main(string[] args)
    {
        // The variants differ only by the Parlot checkout they were built against, so the benchmarks run in this
        // process: BenchmarkDotNet's default toolchain would rebuild the project without that checkout.
        var parlot = typeof(Parlot.Fluent.Parser<>).Assembly;
        var metadata = typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(x => x.Key, x => x.Value);

        Console.WriteLine($"Variant: {metadata.GetValueOrDefault("ParlotVariant")}");
        Console.WriteLine($"Parlot checkout: {metadata.GetValueOrDefault("ParlotRoot")} ({metadata.GetValueOrDefault("ParlotCommit")})");
        Console.WriteLine($"Parlot assembly: {parlot.Location} MVID {parlot.ManifestModule.ModuleVersionId}");

        if (args.Contains("--validate"))
        {
            Validation.Run();
            return 0;
        }

        var job = Job.Default
            .WithToolchain(InProcessEmitToolchain.Instance)
            .WithWarmupCount(int.Parse(Environment.GetEnvironmentVariable("BENCH_WARMUP") ?? "6"))
            .WithIterationCount(int.Parse(Environment.GetEnvironmentVariable("BENCH_ITERATIONS") ?? "20"))
            .WithIterationTime(Perfolizer.Horology.TimeInterval.FromMilliseconds(250));

        var config = ManualConfig.CreateEmpty()
            .AddJob(job)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddColumnProvider(DefaultColumnProviders.Instance)
            .AddColumn(StatisticColumn.Median)
            .AddExporter(JsonExporter.Full)
            .AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        return 0;
    }
}
