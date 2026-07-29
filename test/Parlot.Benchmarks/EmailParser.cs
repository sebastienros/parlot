using System.Collections.Generic;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// A simple email parser that matches email addresses.
/// Pattern: word+ @ word+ . word+ (simplified email format)
/// </summary>
public static partial class EmailParser
{
    public static readonly Parser<char> Dot = Literals.Char('.');
    public static readonly Parser<char> Plus = Literals.Char('+');
    public static readonly Parser<char> Minus = Literals.Char('-');
    public static readonly Parser<char> At = Literals.Char('@');
    public static readonly Parser<TextSpan> WordChar = Literals.Pattern(char.IsLetterOrDigit);
    public static readonly Parser<IReadOnlyList<char>> WordDotPlusMinus = OneOrMany(OneOf(WordChar.Then(static x => 'w'), Dot, Plus, Minus));
    public static readonly Parser<IReadOnlyList<char>> WordDotMinus = OneOrMany(OneOf(WordChar.Then(static x => 'w'), Dot, Minus));
    public static readonly Parser<IReadOnlyList<char>> WordMinus = OneOrMany(OneOf(WordChar.Then(static x => 'w'), Minus));

    /// <summary>
    /// Parses email addresses like "user.name+tag@domain.com"
    /// </summary>
    public static readonly Parser<TextSpan> Parser = Capture(WordDotPlusMinus.And(At).And(WordMinus).And(Dot).And(WordDotMinus));

    /// <summary>
    /// Source-generated email parser for benchmarking.
    /// </summary>
    [GenerateParser]
    public static Parser<TextSpan> GeneratedParser()
    {
        return Parser;
    }
}
