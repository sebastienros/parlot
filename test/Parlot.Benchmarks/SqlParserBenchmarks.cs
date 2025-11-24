using System;
using System.Diagnostics.Tracing;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing.Parsers;
using Parlot.Tests.Sql;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
// [Config(typeof(CustomConfig))]
public class SqlParserBenchmarks
{
    // private class CustomConfig : ManualConfig
    // {
    //     public CustomConfig()
    //     {
    //         AddJob(Job.ShortRun);

    //         var providers = new[]
    //         {
    //             new EventPipeProvider(ClrTraceEventParser.ProviderName, EventLevel.Verbose,
    //                 (long) (
    //                 ClrTraceEventParser.Keywords.GCAllObjectAllocation
    //                 ))
    //         };

    //         AddDiagnoser(new EventPipeProfiler(providers: providers));
    //     }
    // }

    [Params(
        "select a where a not like '%foo%'"
        // "select a where b = (select Avg(c) from d)"
    )]
    public string Sql { get; set; } = string.Empty;

    [Benchmark]
    public bool ParseExpression()
    {
        var result = SqlParser.TryParse(Sql, out var statementList, out var error);

        if (statementList is null || error is not null)
        {
            throw new InvalidOperationException($"Parsing failed: {error}");
        }

        return result;
    }
}
