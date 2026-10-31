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

    // Int128 goes through the generic TryParseRadix<TNumber>, with and without an overflow
    private static readonly Parser<Int128> _hexadecimalInt128 = Literals.Hexadecimal<Int128>();

    private const string HexadecimalText = "deadbeef";
    private const string OctalText = "33653337357";
    private const string BinaryText = "11011110101011011011111011101111";
    private const uint Expected = 0xdeadbeef;

    private readonly ParseContext _hexadecimalContext = new(new Scanner(HexadecimalText));
    private readonly ParseContext _octalContext = new(new Scanner(OctalText));
    private readonly ParseContext _binaryContext = new(new Scanner(BinaryText));
    private readonly ParseContext _int128Context = new(new Scanner("7fffffffffffffffffffffffffffffff"));
    private readonly ParseContext _int128OverflowContext = new(new Scanner("80000000000000000000000000000000"));

    [GlobalSetup]
    public void Setup()
    {
        if (Hexadecimal() != Expected) throw new InvalidOperationException(nameof(Hexadecimal));
        if (Octal() != Expected) throw new InvalidOperationException(nameof(Octal));
        if (Binary() != Expected) throw new InvalidOperationException(nameof(Binary));
        if (HexadecimalInt128() != Int128.MaxValue) throw new InvalidOperationException(nameof(HexadecimalInt128));
        if (HexadecimalInt128Overflow()) throw new InvalidOperationException(nameof(HexadecimalInt128Overflow));
    }

    [Benchmark]
    public Int128 HexadecimalInt128()
    {
        _int128Context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        var result = new ParseResult<Int128>();
        _hexadecimalInt128.Parse(_int128Context, ref result);
        return result.Value;
    }

    [Benchmark]
    public bool HexadecimalInt128Overflow()
    {
        _int128OverflowContext.Scanner.Cursor.ResetPosition(TextPosition.Start);
        var result = new ParseResult<Int128>();
        return _hexadecimalInt128.Parse(_int128OverflowContext, ref result);
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
