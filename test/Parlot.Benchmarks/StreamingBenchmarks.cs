#nullable enable
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using Parlot.Tests.AccessLog;

namespace Parlot.Benchmarks;

/// <summary>
/// Compares parsing from a <see cref="TextReader"/> with reading the whole text first.
/// </summary>
/// <remarks>
/// The grammar counts the failed records of a log instead of building a model, so the results measure the parsers.
/// </remarks>
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
public class StreamingBenchmarks
{
    private static readonly string[] Levels = ["DEBUG", "INFO", "WARN", "ERROR"];
    private static readonly string[] Components = ["http.server", "db", "cache.redis", "auth", "jobs.scheduler"];
    private static readonly string[] Methods = ["GET", "POST", "PUT", "DELETE"];
    private static readonly int[] Statuses = [200, 201, 204, 301, 404, 500, 503];

    private string _log = null!;

    /// <summary>
    /// The number of records in the log, one per line.
    /// </summary>
    [Params(1000, 10000, 100000)]
    public int Count { get; set; }

    /// <summary>
    /// The number of failed records in the log.
    /// </summary>
    public int Expected { get; private set; }

    [GlobalSetup]
    public void Setup()
    {
        (_log, Expected) = CreateLog(Count);
    }

    /// <summary>
    /// Creates a log of <paramref name="count"/> records, and returns it with the number of failed records.
    /// </summary>
    public static (string Log, int Failed) CreateLog(int count)
    {
        var builder = new StringBuilder();
        var failed = 0;

        for (var i = 0; i < count; i++)
        {
            var level = Levels[i % Levels.Length];
            var status = Statuses[i % Statuses.Length];
            var path = (i % 3) switch
            {
                0 => $"/api/items/{i}",
                1 => $"/api/users/{i % 97}/orders?page={i % 5}",
                _ => "/health",
            };
            var message = i % 16 == 0 ? $"client said \\\"retry {i}\\\"" : $"request {i} completed";

            if (i > 0)
            {
                builder.Append('\n');
            }

            builder.Append(CultureInfo.InvariantCulture,
                $"2026-10-02T{i / 3600 % 24:00}:{i / 60 % 60:00}:{i % 60:00}.{i % 1000:000}Z {level} [{Components[i % Components.Length]}] {Methods[i % Methods.Length]} {path} {status} {i % 1000}.{i % 100:00}ms \"{message}\"");

            if (level == "ERROR" || status >= 500)
            {
                failed++;
            }
        }

        return (builder.ToString(), failed);
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Document")]
    public int Document_ReadToEnd()
    {
        using var reader = new ForwardOnlyReader(_log);
        return LogParser.Log.Parse(reader.ReadToEnd());
    }

    [Benchmark, BenchmarkCategory("Document")]
    public async Task<int> Document_TryParseAsync()
    {
        using var reader = new ForwardOnlyReader(_log);
        var (_, value) = await LogParser.Log.TryParseAsync(reader);
        return value;
    }

    [Benchmark, BenchmarkCategory("Document")]
    public int Document_Parse()
    {
        using var reader = new ForwardOnlyReader(_log);
        return LogParser.Log.Parse(reader);
    }

    [Benchmark, BenchmarkCategory("Document")]
    public int Document_Generated_ReadToEnd()
    {
        using var reader = new ForwardOnlyReader(_log);
        _ = GeneratedParsers.TryParseLog(reader.ReadToEnd(), out var value);
        return value;
    }

#if GENERATED_READER
    [Benchmark, BenchmarkCategory("Document")]
    public int Document_Generated_Parse()
    {
        using var reader = new ForwardOnlyReader(_log);
        _ = GeneratedParsers.TryParseLog(reader, out var value);
        return value;
    }
#endif

    [Benchmark(Baseline = true), BenchmarkCategory("Lines")]
    public int Lines_ReadLine()
    {
        using var reader = new ForwardOnlyReader(_log);
        var failed = 0;

        while (reader.ReadLine() is { } line)
        {
            failed += LogParser.Record.Parse(line);
        }

        return failed;
    }

    [Benchmark, BenchmarkCategory("Lines")]
    public async Task<int> Lines_ParseManyAsync()
    {
        using var reader = new ForwardOnlyReader(_log);
        var failed = 0;

        await foreach (var value in LogParser.Record.ParseManyAsync(reader))
        {
            failed += value;
        }

        return failed;
    }

    [Benchmark, BenchmarkCategory("Lines")]
    public async Task<int> Lines_ParseManyAsync_Delimited()
    {
        using var reader = new ForwardOnlyReader(_log);
        var failed = 0;

        await foreach (var value in LogParser.Record.ParseManyAsync(reader, '\n'))
        {
            failed += value;
        }

        return failed;
    }

    /// <summary>
    /// A reader which isn't a <see cref="StringReader"/>, so the text is read like from a file or a network connection.
    /// </summary>
    private sealed class ForwardOnlyReader : TextReader
    {
        private readonly StringReader _reader;

        public ForwardOnlyReader(string text)
        {
            _reader = new StringReader(text);
        }

        public override int Peek() => _reader.Peek();

        public override int Read() => _reader.Read();

        public override int Read(char[] buffer, int index, int count) => _reader.Read(buffer, index, count);

        public override string? ReadLine() => _reader.ReadLine();

        public override string ReadToEnd() => _reader.ReadToEnd();

        public override Task<int> ReadAsync(char[] buffer, int index, int count) => _reader.ReadAsync(buffer, index, count);

        public override ValueTask<int> ReadAsync(System.Memory<char> buffer, System.Threading.CancellationToken cancellationToken = default) => _reader.ReadAsync(buffer, cancellationToken);
    }
}
