#nullable enable
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using Parlot.Tests.Json;

namespace Parlot.Benchmarks;

/// <summary>
/// Compares parsing from a <see cref="TextReader"/> with reading the whole text first.
/// </summary>
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
public class StreamingBenchmarks
{
    private string _document = null!;
    private string _lines = null!;

    /// <summary>
    /// The number of values in the document, and the number of lines.
    /// </summary>
    [Params(1000, 10000, 100000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var values = Enumerable.Range(0, Count).Select(static i => $"{{\"id\":\"{i}\",\"name\":\"{new string('n', i % 32)}\",\"tags\":[\"a\",\"b{i % 7}\"]}}").ToArray();

        _document = "[" + string.Join(",", values) + "]";
        _lines = string.Join("\n", values) + "\n";
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Document")]
    public IJson? Document_ReadToEnd()
    {
        using var reader = new ForwardOnlyReader(_document);
        return JsonParser.Json.Parse(reader.ReadToEnd());
    }

    [Benchmark, BenchmarkCategory("Document")]
    public async Task<IJson?> Document_TryParseAsync()
    {
        using var reader = new ForwardOnlyReader(_document);
        var (_, value) = await JsonParser.Json.TryParseAsync(reader);
        return value;
    }

    [Benchmark, BenchmarkCategory("Document")]
    public IJson? Document_Parse()
    {
        using var reader = new ForwardOnlyReader(_document);
        return JsonParser.Json.Parse(reader);
    }

#if GENERATED_READER
    [Benchmark, BenchmarkCategory("Document")]
    public IJson? Document_Generated_ReadToEnd()
    {
        using var reader = new ForwardOnlyReader(_document);
        _ = GeneratedParsers.TryParseJson(reader.ReadToEnd(), out var value);
        return value;
    }

    [Benchmark, BenchmarkCategory("Document")]
    public IJson? Document_Generated_Parse()
    {
        using var reader = new ForwardOnlyReader(_document);
        _ = GeneratedParsers.TryParseJson(reader, out var value);
        return value;
    }
#endif

    [Benchmark(Baseline = true), BenchmarkCategory("Lines")]
    public int Lines_ReadLine()
    {
        using var reader = new ForwardOnlyReader(_lines);
        var count = 0;

        while (reader.ReadLine() is { } line)
        {
            if (JsonParser.Json.Parse(line) != null)
            {
                count++;
            }
        }

        return count;
    }

    [Benchmark, BenchmarkCategory("Lines")]
    public async Task<int> Lines_ParseManyAsync()
    {
        using var reader = new ForwardOnlyReader(_lines);
        var count = 0;

        await foreach (var _ in JsonParser.Json.ParseManyAsync(reader))
        {
            count++;
        }

        return count;
    }

    [Benchmark, BenchmarkCategory("Lines")]
    public async Task<int> Lines_ParseManyAsync_Delimited()
    {
        using var reader = new ForwardOnlyReader(_lines);
        var count = 0;

        await foreach (var _ in JsonParser.Json.ParseManyAsync(reader, '\n'))
        {
            count++;
        }

        return count;
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
