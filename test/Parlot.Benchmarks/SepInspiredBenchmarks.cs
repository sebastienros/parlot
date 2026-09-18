using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using BenchmarkDotNet.Attributes;
using Parlot.Tests.Json;
using Parlot.Tests.Sql;

namespace Parlot.Benchmarks;

// Deterministic corpus; use the same binaries/data for before and after runs.
[MemoryDiagnoser, ShortRunJob]
public class SepGrammarBenchmarks
{
    private string _json;
    private string _sql;

    [Params(8, 256)]
    public int TokenLength { get; set; }

    [Params(false, true)]
    public bool Escaped { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var identifier = new string('a', TokenLength);
        var value = Escaped ? identifier + @"\n" + identifier : identifier;
        var json = new StringBuilder("[");
        for (var i = 0; i < 32; i++)
        {
            if (i != 0) json.Append(',');
            json.Append("\n  {\"name\":\"").Append(value).Append("\",\"escaped\":\"a\\nb\"}");
        }
        _json = json.Append("\n]").ToString();
        _sql = $"select {identifier}, b from table1 where b not like '%{value}%'";
        if (JsonRuntime() is null || JsonGenerated() is null || !SqlRuntime() || !SqlGenerated())
            throw new InvalidOperationException("Invalid benchmark corpus.");
    }

    [Benchmark] public IJson JsonRuntime() => JsonParser.Parse(_json);
    [Benchmark] public IJson JsonGenerated() => GeneratedParsers.TryParseJson(_json, out var value) ? value : throw new InvalidOperationException();
    [Benchmark] public bool SqlRuntime() => SqlParser.TryParse(_sql, out _, out _);
    [Benchmark] public bool SqlGenerated() => SqlParser.TryParse(_sql, out _);
}

[MemoryDiagnoser, ShortRunJob]
public class SepCursorBenchmarks
{
    private Cursor _cursor;
    [Params(1, 8, 32, 256, 4096)] public int Length { get; set; }
    [Params(false, true)] public bool NewLines { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var chars = new string('a', Length).ToCharArray();
        if (NewLines)
            for (var i = 5; i < chars.Length; i += 16) chars[i] = '\n';
        _cursor = new Cursor(new string(chars) + "!");
    }

    [Benchmark]
    public TextPosition Advance()
    {
        _cursor.ResetPosition(TextPosition.Start);
        _cursor.Advance(Length);
        return _cursor.Position;
    }
}

[MemoryDiagnoser, ShortRunJob]
public class SepScanBenchmarks
{
    private static readonly SearchValues<char> _special = System.Buffers.SearchValues.Create("\"\\\r\n");
    private string _text;
    [Params(8, 64, 1024)] public int Length { get; set; }
    [Params(false, true)] public bool Unicode { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        foreach (var size in new[] { 0, 1, 7, 8, 9, 15, 16, 17, 31, 32, 33, 65 })
        {
            foreach (var fill in new[] { 'a', '\u0122', '\uffff' })
            {
                var chars = new string(fill, size).ToCharArray();
                for (var index = -1; index < size; index++)
                {
                    foreach (var special in new[] { '"', '\\', '\r', '\n' })
                    {
                        if (index >= 0) chars[index] = special;
                        _text = new string(chars);
                        if (Scalar() != index || SearchValues() != index || VectorMask() != index || NarrowedVectorMask() != index)
                            throw new InvalidOperationException("Scan boundary mismatch.");
                        if (index >= 0) chars[index] = fill;
                    }
                }
            }
        }
        // U+0122 has the same low byte as a quote: narrowing must saturate.
        _text = new string(Unicode ? '\u0122' : 'a', Length) + '"';
        if (Scalar() != Length || SearchValues() != Length || VectorMask() != Length || NarrowedVectorMask() != Length)
            throw new InvalidOperationException("Scan mismatch.");
    }

    [Benchmark(Baseline = true)]
    public int Scalar()
    {
        var span = _text.AsSpan();
        for (var i = 0; i < span.Length; i++)
            if (span[i] is '"' or '\\' or '\r' or '\n') return i;
        return -1;
    }

    [Benchmark] public int SearchValues() => _text.AsSpan().IndexOfAny(_special);

    [Benchmark]
    public int VectorMask()
    {
        var span = MemoryMarshal.Cast<char, ushort>(_text.AsSpan());
        var i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            for (; i <= span.Length - Vector128<ushort>.Count; i += Vector128<ushort>.Count)
            {
                var v = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(span), (nuint)i);
                var matches = Vector128.Equals(v, Vector128.Create((ushort)'"')) |
                    Vector128.Equals(v, Vector128.Create((ushort)'\\')) |
                    Vector128.Equals(v, Vector128.Create((ushort)'\r')) |
                    Vector128.Equals(v, Vector128.Create((ushort)'\n'));
                var mask = matches.ExtractMostSignificantBits();
                if (mask != 0) return i + System.Numerics.BitOperations.TrailingZeroCount(mask);
            }
        }
        return Tail(i);
    }

    [Benchmark]
    public int NarrowedVectorMask()
    {
        var span = MemoryMarshal.Cast<char, ushort>(_text.AsSpan());
        var i = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            for (; i <= span.Length - 16; i += 16)
            {
                var a = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(span), (nuint)i);
                var b = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(span), (nuint)(i + 8));
                var v = Vector128.NarrowWithSaturation(a, b);
                var matches = Vector128.Equals(v, Vector128.Create((byte)'"')) |
                    Vector128.Equals(v, Vector128.Create((byte)'\\')) |
                    Vector128.Equals(v, Vector128.Create((byte)'\r')) |
                    Vector128.Equals(v, Vector128.Create((byte)'\n'));
                var mask = matches.ExtractMostSignificantBits();
                if (mask != 0) return i + System.Numerics.BitOperations.TrailingZeroCount(mask);
            }
        }
        return Tail(i);
    }

    private int Tail(int start)
    {
        for (var i = start; i < _text.Length; i++)
            if (_text[i] is '"' or '\\' or '\r' or '\n') return i;
        return -1;
    }
}

// Pool lifetime is ONE document. Includes pool creation, misses, hits and discarded state.
[MemoryDiagnoser, ShortRunJob]
public class SepStringAllocationBenchmarks
{
    private TextSpan[] _tokens;
    // 0: unique; 4: cyclic repetition; 1: consecutive repetition.
    [Params(0, 4, 1)] public int Distinct { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _tokens = new TextSpan[256];
        for (var i = 0; i < _tokens.Length; i++)
        {
            var text = "[identifier" + (Distinct == 0 ? i : i % Distinct).ToString(System.Globalization.CultureInfo.InvariantCulture) + "]";
            _tokens[i] = new TextSpan(text, 1, text.Length - 2);
        }
    }

    [Benchmark(Baseline = true)]
    public int Materialize()
    {
        var sum = 0;
        foreach (var token in _tokens) sum += token.ToString().Length;
        return sum;
    }

    [Benchmark]
    public int DocumentPool()
    {
        var pool = new Dictionary<TextSpan, string>();
        var sum = 0;
        foreach (var token in _tokens)
        {
            if (!pool.TryGetValue(token, out var value))
            {
                value = token.ToString();
                pool.Add(token, value);
            }
            sum += value.Length;
        }
        return sum;
    }

    [Benchmark]
    public int LastStringCache()
    {
        string last = null;
        var sum = 0;
        foreach (var token in _tokens)
        {
            if (last is null || !token.Span.SequenceEqual(last.AsSpan())) last = token.ToString();
            sum += last.Length;
        }
        return sum;
    }

    [Benchmark]
    public int KeepTextSpan()
    {
        var sum = 0;
        foreach (var token in _tokens) sum += token.Length;
        return sum;
    }
}

[MemoryDiagnoser, ShortRunJob]
public class SepQuotedStringBenchmarks
{
    private Scanner _scanner;
    [Params(0, 6, 8, 16, 128, 1024)] public int Length { get; set; }
    [Params(false, true)] public bool Unicode { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _scanner = new Scanner("\"" + new string(Unicode ? '\u0122' : 'a', Length) + "\"!");
        if (Read() != Length + 2) throw new InvalidOperationException("Quoted string mismatch.");
    }

    [Benchmark]
    public int Read()
    {
        _scanner.Cursor.ResetPosition(TextPosition.Start);
        return _scanner.ReadDoubleQuotedString(out var value) ? value.Length : -1;
    }
}
