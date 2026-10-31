using System;
using System.Collections.Generic;
using System.Linq;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// A choice whose alternatives tell longer prefixes than their first chars only tries the ones the input starts with.
/// </summary>
public class PrefixDispatchTests
{
    private static readonly string[] Keywords = ["if", "implicit", "in", "int", "interface", "internal", "is", "case", "catch", "char", "class", "In", "Int", "Interface", "INT"];

    private static readonly string[] Operators = ["<<=", "<=", "<", "<<", "<>", ">>=", ">=", ">>", ">", "==", "=", "!=", "!", "éé", "éx", "é", "𐀀", "𐀁", "\ud800"];

    [Theory]
    [InlineData(" is", new[] { 6 })]
    [InlineData(" interface", new[] { 2, 3, 4 })]
    [InlineData(" internal", new[] { 2, 3, 5 })]
    [InlineData(" inter", new[] { 2, 3 })]
    [InlineData(" iz", new int[0])]
    [InlineData(" Int", new[] { 11, 12 })]
    [InlineData(" class", new[] { 10 })]
    public void ChoicesOnlyTryTheAlternativesOfThePrefix(string input, int[] expected)
    {
        var alternatives = Keywords.Select(static (word, index) => Terms.Keyword(word).Then(index)).ToArray();

        Assert.Equal(expected, Entered(OneOf(alternatives), alternatives, input));
    }

    [Theory]
    [InlineData("if", new[] { 0 })]
    [InlineData("int", new[] { 1, 2 })]
    [InlineData("is", new[] { 2 })]
    [InlineData("iz", new[] { 2 })]
    [InlineData("x", new[] { 2 })]
    public void AlternativesWithoutLongerPrefixesAreTriedForEveryPrefix(string input, int[] expected)
    {
        Parser<int>[] alternatives = [Terms.Keyword("if").Then(0), Terms.Keyword("in").Then(1), Terms.Pattern(static c => char.IsLetter(c)).Then(2), Terms.Keyword("int").Then(3), Terms.Keyword("is").Then(4)];

        Assert.Equal(expected, Entered(OneOf(alternatives), alternatives, input));
    }

    [Fact]
    public void ChoicesWithoutLongerPrefixesTryEveryAlternativeOfTheChar()
    {
        // Case-insensitive texts only tell their first chars
        var insensitive = Keywords.Select(static (word, index) => Terms.Keyword(word, caseInsensitive: true).Then(index)).ToArray();

        Assert.Equal([0, 1, 2, 3, 4, 5, 6], Entered(OneOf(insensitive), insensitive, " is"));

        // At most two alternatives for each char
        Parser<int>[] small = [Terms.Keyword("if").Then(0), Terms.Keyword("is").Then(1), Terms.Keyword("else").Then(2)];

        Assert.Equal([0, 1], Entered(OneOf(small), small, " is"));
    }

    [Fact]
    public void WrappedKeywordsAgreeWithAnOrderedReference()
    {
        // A fallback among the keywords is tried at its position for every prefix
        Parser<int>[] alternatives =
        [
            .. Keywords.Take(11).Select(static (word, index) => Terms.Keyword(word).Then(index)),
            Terms.Pattern(static c => c is >= 'a' and <= 'z').Then(-1),
            .. Keywords.Skip(11).Select(static (word, index) => Terms.Keyword(word).Then(11 + index)),
        ];

        AssertOrdered(alternatives, Keywords);
        AssertOrdered(Keywords.Select(static word => Terms.Keyword(word)).ToArray(), Keywords);
        // A choice skips the white space of Terms before its alternatives, which a capture would then miss
        AssertOrdered(Keywords.Select(static word => Capture(Literals.Keyword(word)).Then(static span => span.ToString())).ToArray(), Keywords);
    }

    [Fact]
    public void TextsAgreeWithAnOrderedReference()
    {
        AssertOrdered(Operators.Select(static (op, index) => Terms.Text(op).Then(index)).ToArray(), Operators);

        // An operator not followed by a space gives way to the next one
        AssertOrdered(Operators.Select(static (op, index) => Literals.Text(op).AndSkip(Literals.Char(' ')).Then(index)).ToArray(), Operators);
    }

    [Fact]
    public void NestedAndDeferredChoicesAgreeWithAnOrderedReference()
    {
        Parser<string>[] nested = [Terms.Keyword("in").Or(Terms.Keyword("int")).Or(Terms.Keyword("interface")), Terms.Keyword("internal"), Terms.Keyword("if"), Terms.Keyword("is")];

        AssertOrdered(nested, Keywords);

        // The deferred parser is set after the choice is built
        var deferred = Deferred<string>();
        Parser<string>[] alternatives = [deferred, Terms.Keyword("integer"), Terms.Keyword("i"), Terms.Pattern(static c => char.IsLetter(c)).Then(static _ => "id")];
        var choice = OneOf(alternatives);
        deferred.Parser = OneOf(Keywords.Select(static word => Terms.Keyword(word)).ToArray());

        AssertOrdered(choice, alternatives, [.. Keywords, "integer", "i"]);
    }

    private static void AssertOrdered<T>(Parser<T>[] alternatives, string[] words) => AssertOrdered(OneOf(alternatives), alternatives, words);

    private static void AssertOrdered<T>(Parser<T> choice, Parser<T>[] alternatives, string[] words)
    {
        var ordered = new Ordered<T>(alternatives);

        foreach (var input in PrefixInputs(words))
        {
            foreach (var text in new[] { input, " \r\n" + input })
            {
                Assert.Equal(Run(ordered, text), Run(choice, text));
            }
        }
    }

    private static (bool Success, T Value, int Offset) Run<T>(Parser<T> parser, string text)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<T>();
        var success = parser.Parse(context, ref result);

        // The cursor is back at the start on failure
        return (success, success ? result.Value : default, context.Scanner.Cursor.Offset);
    }

    private static List<int> Entered<T>(Parser<T> choice, Parser<T>[] alternatives, string text)
    {
        var entered = new List<int>();
        var context = new ParseContext(new Scanner(text))
        {
            OnEnterParser = (parser, _) =>
            {
                var index = Array.FindIndex(alternatives, alternative => ReferenceEquals(alternative, parser));

                if (index >= 0)
                {
                    entered.Add(index);
                }
            },
        };

        var result = new ParseResult<T>();
        choice.Parse(context, ref result);

        return entered;
    }

    // Each alternative, shortened, extended and with each of its chars replaced, then random texts of their chars
    private static List<string> PrefixInputs(string[] words)
    {
        var inputs = new List<string> { "", " ", "x" };
        var alphabet = string.Concat(words) + " xX9é\0";

        foreach (var word in words)
        {
            foreach (var other in words)
            {
                inputs.Add(word + other);
                inputs.Add(word + " " + other);
            }

            inputs.Add(word.Substring(0, word.Length - 1));
            inputs.Add(word.Substring(1));
            inputs.Add(word + "x");
            inputs.Add(word + "9");
            inputs.Add(word + "é");

            for (var offset = 0; offset < word.Length; offset++)
            {
                for (var c = 0; c < 128; c++)
                {
                    inputs.Add(word.Substring(0, offset) + (char)c + word.Substring(offset + 1));
                }
            }
        }

        var random = new Random(431);

        for (var i = 0; i < 5000; i++)
        {
            var input = new char[random.Next(0, 16)];

            for (var j = 0; j < input.Length; j++)
            {
                input[j] = random.Next(8) == 0 ? (char)random.Next(0, 65536) : alphabet[random.Next(alphabet.Length)];
            }

            inputs.Add(new string(input));
        }

        return inputs;
    }

    // Tries the alternatives in order, without lookup
    private sealed class Ordered<T> : Parser<T>
    {
        private readonly Parser<T>[] _parsers;

        public Ordered(Parser<T>[] parsers)
        {
            _parsers = parsers;
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            foreach (var parser in _parsers)
            {
                if (parser.Parse(context, ref result))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
