using System.Collections.Generic;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// A simple email parser that matches email addresses.
/// Pattern: word+ @ word+ . word+ (simplified email format)
/// </summary>
public static partial class EmailParser
{
    /// <summary>
    /// Parses email addresses like "user.name+tag@domain.com"
    /// </summary>
    public static Parser<TextSpan> Parser => RuntimeState.Parser;

    public static partial bool TryParseGenerated(string input, out string value);

    private static class RuntimeState
    {
        private static readonly Parser<char> Dot = Literals.Char('.');
        private static readonly Parser<char> Plus = Literals.Char('+');
        private static readonly Parser<char> Minus = Literals.Char('-');
        private static readonly Parser<char> At = Literals.Char('@');
        private static readonly Parser<TextSpan> WordChar = Literals.Pattern(char.IsLetterOrDigit);
        private static readonly Parser<IReadOnlyList<char>> WordDotPlusMinus =
            OneOrMany(OneOf(WordChar.Then(static _ => 'w'), Dot, Plus, Minus));
        private static readonly Parser<IReadOnlyList<char>> WordDotMinus =
            OneOrMany(OneOf(WordChar.Then(static _ => 'w'), Dot, Minus));
        private static readonly Parser<IReadOnlyList<char>> WordMinus =
            OneOrMany(OneOf(WordChar.Then(static _ => 'w'), Minus));

        internal static readonly Parser<TextSpan> Parser =
            Capture(WordDotPlusMinus.And(At).And(WordMinus).And(Dot).And(WordDotMinus));
    }
}
