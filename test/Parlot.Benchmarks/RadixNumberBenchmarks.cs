using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class RadixNumberBenchmarks
{
    private static readonly Parser<uint> _hexadecimal = Literals.Hexadecimal<uint>();
    private static readonly Parser<uint> _octal = Literals.Octal<uint>();
    private static readonly Parser<uint> _binary = Literals.Binary<uint>();

    private const string HexadecimalText = "deadbeef";
    private const string OctalText = "33653337357";
    private const string BinaryText = "11011110101011011011111011101111";
    private const uint Expected = 0xdeadbeef;

    private readonly ParseContext _hexadecimalContext = new(new Scanner(HexadecimalText));
    private readonly ParseContext _octalContext = new(new Scanner(OctalText));
    private readonly ParseContext _binaryContext = new(new Scanner(BinaryText));

    [GlobalSetup]
    public void Setup()
    {
        if (Hexadecimal() != Expected) throw new InvalidOperationException(nameof(Hexadecimal));
        if (Octal() != Expected) throw new InvalidOperationException(nameof(Octal));
        if (Binary() != Expected) throw new InvalidOperationException(nameof(Binary));
    }

    [Benchmark]
    public uint Hexadecimal()
    {
        _hexadecimalContext.Scanner.Cursor.ResetPosition(TextPosition.Start);
        var result = new ParseResult<uint>();
        _hexadecimal.Parse(_hexadecimalContext, ref result);
        return result.Value;
    }

    [Benchmark]
    public uint Octal()
    {
        _octalContext.Scanner.Cursor.ResetPosition(TextPosition.Start);
        var result = new ParseResult<uint>();
        _octal.Parse(_octalContext, ref result);
        return result.Value;
    }

    [Benchmark]
    public uint Binary()
    {
        _binaryContext.Scanner.Cursor.ResetPosition(TextPosition.Start);
        var result = new ParseResult<uint>();
        _binary.Parse(_binaryContext, ref result);
        return result.Value;
    }
}
