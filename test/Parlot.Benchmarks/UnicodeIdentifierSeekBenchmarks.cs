using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// | Method                 | Mean     | Error     | StdDev   | Gen0   | Allocated |
// |----------------------- |---------:|----------:|---------:|-------:|----------:|
// | Build                  | 14.42 us |  1.968 us | 0.108 us | 0.1984 |  27.78 KB |
// | Parse                  | 29.19 us | 13.892 us | 0.761 us | 0.1221 |  16.62 KB |
// | ParseOtherSeekableChar | 29.80 us | 11.341 us | 0.622 us | 0.1221 |  16.62 KB |

// Before NonAsciiSeekableChar, Build took 20,554 us and allocated 27,141 KB: a table entry for each identifier start char.

// Measures a choice of tokens whose identifier starts with any Unicode letter, like JavaScript's ID_Start: building the
// choice, which builds its lookup table from the chars each alternative can start with, and parsing tokens that mostly
// start with ASCII chars which can't start an identifier.

[MemoryDiagnoser, ShortRunJob]
public class UnicodeIdentifierSeekBenchmarks
{
    // Seven tokens each
    private const int Repeats = 150;

    private static readonly string _start = CreateChars(static c => c is '$' or '_' || char.IsLetter(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.LetterNumber);

    private static readonly string _part = CreateChars(static c => c is '$' or '_' || char.IsLetterOrDigit(c) || CharUnicodeInfo.GetUnicodeCategory(c)
        is UnicodeCategory.LetterNumber or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.ConnectorPunctuation);

    private readonly Parser<TextSpan>[] _alternatives =
    [
        Terms.Text("if").Then(static x => new TextSpan(x)),
        Terms.Text("while").Then(static x => new TextSpan(x)),
        Terms.String(),
        Terms.Char('(').Then(static _ => new TextSpan("(")),
        Terms.Char(')').Then(static _ => new TextSpan(")")),
        Terms.Identifier(_start.AsSpan(), _part.AsSpan()),
    ];

    private Parser<IReadOnlyList<TextSpan>> _tokens = null!;
    private Parser<IReadOnlyList<TextSpan>> _workaround = null!;
    private string _text = null!;

    private static string CreateChars(Func<char, bool> predicate)
    {
        var builder = new StringBuilder();

        for (var c = 0; c <= char.MaxValue; c++)
        {
            if (!char.IsSurrogate((char)c) && predicate((char)c))
            {
                builder.Append((char)c);
            }
        }

        return builder.ToString();
    }

    [GlobalSetup]
    public void Setup()
    {
        _tokens = ZeroOrMany(Build());
        _text = string.Concat(System.Linq.Enumerable.Repeat("(\"a\") if (é) ", Repeats));

        // The identifier declares its ASCII start chars, and that it can start with any other char
        var alternatives = (Parser<TextSpan>[])_alternatives.Clone();
        alternatives[^1] = Terms.Identifier(_start.AsSpan(), _part.AsSpan()).Lookup(true, "$_abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ" + Fluent.OneOf<TextSpan>.OtherSeekableChar);
        _workaround = ZeroOrMany(new OneOf<TextSpan>(alternatives));

        if (_tokens.Parse(_text).Count != Repeats * 7 || _workaround.Parse(_text).Count != Repeats * 7)
        {
            throw new InvalidOperationException("Incorrect token parsing.");
        }
    }

    /// <summary>
    /// Builds the choice, with its lookup table.
    /// </summary>
    [Benchmark]
    public Parser<TextSpan> Build() => new OneOf<TextSpan>(_alternatives);

    [Benchmark]
    public IReadOnlyList<TextSpan> Parse() => _tokens.Parse(_text);

    /// <summary>
    /// Parses with an identifier that declares it can start with any char, the alternative to a table of all its chars.
    /// </summary>
    [Benchmark]
    public IReadOnlyList<TextSpan> ParseOtherSeekableChar() => _workaround.Parse(_text);
}
