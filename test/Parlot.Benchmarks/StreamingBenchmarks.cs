#nullable enable
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using Parlot.Tests.Json;

namespace Parlot.Benchmarks;

/// <summary>
/// Compares parsing from a <see cref="TextReader"/> or a <see cref="Stream"/> with reading the whole text first.
/// </summary>
[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
public class StreamingBenchmarks
{
    private string _document = null!;
    private byte[] _documentBytes = null!;
    private string _lines = null!;
    private byte[] _linesBytes = null!;

    /// <summary>
    /// The number of values in the document, and the number of lines.
    /// </summary>
    [Params(10, 1000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var values = Enumerable.Range(0, Count).Select(static i => $"{{\"id\":\"{i}\",\"name\":\"{new string('n', i % 32)}\",\"tags\":[\"a\",\"b{i % 7}\"]}}").ToArray();

        _document = "[" + string.Join(",", values) + "]";
        _documentBytes = Encoding.UTF8.GetBytes(_document);
        _lines = string.Join("\n", values) + "\n";
        _linesBytes = Encoding.UTF8.GetBytes(_lines);
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
    public async Task<IJson?> Document_Stream_ReadToEnd()
    {
        using var stream = new ForwardOnlyStream(_documentBytes);
        using var reader = new StreamReader(stream);
        return JsonParser.Json.Parse(await reader.ReadToEndAsync());
    }

    [Benchmark, BenchmarkCategory("Document")]
    public async Task<IJson?> Document_Stream_TryParseAsync()
    {
        using var stream = new ForwardOnlyStream(_documentBytes);
        var (_, value) = await JsonParser.Json.TryParseAsync(stream);
        return value;
    }

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
    public async Task<long> Lines_ParseManyAsync()
    {
        using var reader = new ForwardOnlyReader(_lines);
        return await JsonParser.Json.ParseManyAsync(reader, static _ => { });
    }

    [Benchmark, BenchmarkCategory("Lines")]
    public async Task<long> Lines_ParseManyAsync_Delimited()
    {
        using var reader = new ForwardOnlyReader(_lines);
        return await JsonParser.Json.ParseManyAsync(reader, '\n', static _ => { });
    }

    [Benchmark, BenchmarkCategory("Lines")]
    public async Task<long> Lines_Stream_ParseManyAsync()
    {
        using var stream = new ForwardOnlyStream(_linesBytes);
        return await JsonParser.Json.ParseManyAsync(stream, static (_, _) => default);
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

    /// <summary>
    /// A stream which can't seek, so the text is read like from a network connection.
    /// </summary>
    private sealed class ForwardOnlyStream : MemoryStream
    {
        public ForwardOnlyStream(byte[] buffer) : base(buffer, writable: false)
        {
        }

        public override bool CanSeek => false;
    }
}
