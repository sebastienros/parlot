using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class SecurityBoundaryTests
{
    [Fact]
    public void RepetitionParsersStopWhenTheInnerParserMakesNoProgress()
    {
        var optionalValue = ZeroOrOne(Literals.Char('a'));

        Assert.Equal(3, ZeroOrMany(optionalValue).Parse("aaa")!.Count);
        Assert.Equal(3, OneOrMany(optionalValue).Parse("aaa")!.Count);
        Assert.False(OneOrMany(optionalValue).TryParse("", out _));

        var optionalSeparator = ZeroOrOne(Literals.Char(','));
        Assert.Equal(3, Separated(optionalSeparator, optionalValue).Parse("aaa")!.Count);
        Assert.False(Separated(optionalSeparator, optionalValue).TryParse("", out _));
    }

    [Fact]
    public void CancellationRequestedDuringParsingIsObserved()
    {
        var parser = ZeroOrMany(Terms.Integer());
        var input = string.Join(" ", Enumerable.Range(0, 10_000));
        using var cancellation = new CancellationTokenSource();
        var context = new ParseContext(new Scanner(input), cancellation.Token);
        var enteredParsers = 0;
        context.OnEnterParser = (_, _) =>
        {
            if (++enteredParsers == 128)
            {
                cancellation.Cancel();
            }
        };
        var result = new ParseResult<IReadOnlyList<long>>();

        try
        {
            parser.Parse(context, ref result);
            Assert.Fail("Parsing should observe cancellation requested during the operation.");
        }
        catch (OperationCanceledException)
        {
        }
    }

    [Fact]
    public void TruncatedQuotedStringRestoresAtEveryBoundary()
    {
        const string text = "\"a\\u1234\\\\\\\"z\"";

        for (var length = 0; length < text.Length; length++)
        {
            var scanner = new Scanner(text[..length]);

            Assert.False(scanner.ReadQuotedString());
            Assert.Equal(0, scanner.Cursor.Offset);
        }

        Assert.True(new Scanner(text).ReadQuotedString());
    }

    [Fact]
    public void FailedCombinatorsRestoreTheCursorForShortAdversarialInputs()
    {
        Parser<string>[] parsers =
        [
            Literals.Text("abc"),
            Terms.Text("abc"),
            Between(Literals.Char('['), Literals.Text("abc"), Literals.Char(']')),
            Literals.Text("ab").AndSkip(Literals.Char('!'))
        ];
        ReadOnlySpan<char> alphabet = [' ', 'a', 'b', 'c', '!', '[', ']'];

        for (var length = 0; length <= 4; length++)
        {
            var combinations = (int)Math.Pow(alphabet.Length, length);
            for (var value = 0; value < combinations; value++)
            {
                var input = CreateInput(alphabet, length, value);
                foreach (var parser in parsers)
                {
                    var context = new ParseContext(new Scanner(input));
                    var result = new ParseResult<string>();

                    if (!parser.Parse(context, ref result))
                    {
                        Assert.Equal(0, context.Scanner.Cursor.Offset);
                    }
                }
            }
        }
    }

    private static string CreateInput(ReadOnlySpan<char> alphabet, int length, int value)
    {
        var buffer = new char[length];
        for (var index = 0; index < buffer.Length; index++)
        {
            buffer[index] = alphabet[value % alphabet.Length];
            value /= alphabet.Length;
        }

        return new string(buffer);
    }
}
