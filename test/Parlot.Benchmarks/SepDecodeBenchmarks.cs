using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Parlot.Benchmarks;

// The same escape semantics as Character.DecodeStringInternal; all inputs are validated strings.
[MemoryDiagnoser, ShortRunJob]
public class SepDecodeBenchmarks
{
    private string _text;
    [Params(32, 1024)] public int Length { get; set; }
    [Params(4, 128)] public int EscapeInterval { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var text = new System.Text.StringBuilder();
        for (var i = 0; i < Length; i++) text.Append(i % EscapeInterval == 0 ? "\\n" : "a");
        _text = text.ToString();
        foreach (var sample in new[] { _text, "", "abc", "\\n\\r\\t\\0\\a\\b\\f\\v\\'\\\"\\\\", "a\\u1234b\\x41c" })
        {
            var expected = Character.DecodeStringInternal(sample);
            if (DecodeBulk(sample, false) != expected || DecodeBulk(sample, true) != expected || DecodeExact(sample) != expected)
                throw new InvalidOperationException("Decoder mismatch.");
        }
    }

    [Benchmark(Baseline = true)] public string Current() => Character.DecodeStringInternal(_text);
    [Benchmark] public string BulkCopy() => DecodeBulk(_text, false);
    [Benchmark] public string BulkCopySkipInit() => DecodeBulk(_text, true);
    [Benchmark] public string ExactString() => DecodeExact(_text);

    private static string DecodeBulk(string text, bool skipInit) => skipInit ? DecodeUninitialized(text) : DecodeInitialized(text);

    private static string DecodeInitialized(string text)
    {
        char[] rented = null;
        Span<char> buffer = text.Length <= 128 ? stackalloc char[text.Length] : (rented = ArrayPool<char>.Shared.Rent(text.Length));
        try { return buffer[..DecodeInto(text, buffer)].ToString(); }
        finally { if (rented is not null) ArrayPool<char>.Shared.Return(rented); }
    }

    [SkipLocalsInit]
    private static string DecodeUninitialized(string text)
    {
        char[] rented = null;
        Span<char> buffer = text.Length <= 128 ? stackalloc char[text.Length] : (rented = ArrayPool<char>.Shared.Rent(text.Length));
        try { return buffer[..DecodeInto(text, buffer)].ToString(); }
        finally { if (rented is not null) ArrayPool<char>.Shared.Return(rented); }
    }

    private static string DecodeExact(string text)
    {
        var length = text.Length;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\') continue;
            var start = i;
            _ = ReadEscape(text, ref i);
            length -= i - start;
        }
        return string.Create(length, text, static (output, input) => DecodeInto(input, output));
    }

    private static int DecodeInto(ReadOnlySpan<char> input, Span<char> output)
    {
        var written = 0;
        while (!input.IsEmpty)
        {
            var next = input.IndexOf('\\');
            if (next < 0)
            {
                input.CopyTo(output[written..]);
                return written + input.Length;
            }
            input[..next].CopyTo(output[written..]);
            written += next;
            var index = next;
            output[written++] = ReadEscape(input, ref index);
            input = input[(index + 1)..];
        }
        return written;
    }

    private static char ReadEscape(ReadOnlySpan<char> input, ref int index)
    {
        var c = input[++index];
        switch (c)
        {
            case '0': return '\0';
            case 'a': return '\a';
            case 'b': return '\b';
            case 'f': return '\f';
            case 'n': return '\n';
            case 'r': return '\r';
            case 't': return '\t';
            case 'v': return '\v';
            case 'u':
            case 'x':
                c = Character.ScanHexEscape(input[index..], out var length);
                index += length;
                return c;
            default: return c;
        }
    }
}
