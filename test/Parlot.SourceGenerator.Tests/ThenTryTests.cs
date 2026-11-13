#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// The generated ThenTry converts and rejects the values the runtime one does, and leaves the cursor where it does.
/// </summary>
public class ThenTryTests
{
    [Theory]
    [InlineData("foo", "foo@0-3")]
    [InlineData("  foo ", "foo@2-5")]
    [InlineData("\n\tiffy", "iffy@2-6")]
    [InlineData("if", null)]
    [InlineData("  else", null)]
    [InlineData("  1", null)]
    [InlineData("", null)]
    public void Identifier_Is_Converted_Or_Rejected(string input, string? expected)
    {
        var runtime = ThenTryGrammars.CreateIdentifier();

        Assert.Equal(expected != null, runtime.TryParse(input, out var runtimeValue));
        Assert.Equal(expected != null, ThenTryGrammars.TryParseIdentifier(input, out var value));

        if (expected != null)
        {
            Assert.Equal(expected, runtimeValue);
            Assert.Equal(expected, value);
        }
    }

    [Theory]
    [InlineData("foo", "foo@0-3")]
    [InlineData(" if", "if")]
    [InlineData(" iffy", "iffy@1-5")]
    [InlineData("else", "else")]
    [InlineData(" 12", "12")]
    [InlineData("(", "(")]
    [InlineData(" )", null)]
    public void Rejected_Value_Lets_The_Next_Alternative_Match(string input, string? expected)
    {
        Assert.Equal(expected != null, ThenTryGrammars.CreateChoice().TryParse(input, out var runtimeValue));
        Assert.Equal(expected != null, ThenTryGrammars.TryParseChoice(input, out var value));

        if (expected != null)
        {
            Assert.Equal(expected, runtimeValue);
            Assert.Equal(expected, value);
        }
    }

    [Theory]
    [InlineData("a if b else (1", "a@0-1,if,b@5-6,else,(,1")]
    [InlineData("", "")]
    [InlineData(" if  ifx\n else", "if,ifx@5-8,else")]
    [InlineData("a )", null)]
    public void List_Matches_From_A_String_And_A_Reader(string input, string? expected)
    {
        Assert.Equal(expected != null, ZeroOrMany(ThenTryGrammars.CreateChoice()).Eof().TryParse(input, out var runtimeValue));
        Assert.Equal(expected != null, ThenTryGrammars.TryParseList(input, out var value));
        Assert.Equal(expected != null, ThenTryGrammars.TryParseList(new StringReader(input), out var readValue));

        if (expected != null)
        {
            Assert.Equal(expected, string.Join(",", runtimeValue!));
            Assert.Equal(expected, string.Join(",", value));
            Assert.Equal(expected, string.Join(",", readValue));
        }
    }

    [Theory]
    [InlineData("0", (byte)0)]
    [InlineData("255", (byte)255)]
    [InlineData("256", null)]
    [InlineData(" 1", null)]
    public void Tuple_Conversion_Is_Converted_Or_Rejected(string input, byte? expected)
    {
        Assert.Equal(expected != null, ThenTryGrammars.CreateByte().TryParse(input, out var runtimeValue));
        Assert.Equal(expected != null, ThenTryGrammars.TryParseByte(input, out var value));

        if (expected != null)
        {
            Assert.Equal(expected, runtimeValue);
            Assert.Equal(expected, value);
        }
    }

    [Theory]
    [InlineData("[12]", true)]
    [InlineData("[ 12 ]", false)]
    [InlineData("[256]", false)]
    public void Value_That_Isnt_Used_Is_Still_Rejected(string input, bool expected)
    {
        Assert.Equal(expected, ThenTryGrammars.CreateDiscarded().TryParse(input, out _));
        Assert.Equal(expected, ThenTryGrammars.TryParseDiscarded(input, out var value));

        if (expected)
        {
            Assert.Equal('[', value);
        }
    }

    [Theory]
    [InlineData("2147483647", 2147483647)]
    [InlineData(" -5", -5)]
    [InlineData("2147483648", null)]
    public void Method_Group_Conversion_Is_Generated(string input, int? expected)
    {
        Assert.Equal(expected != null, ThenTryGrammars.CreateMethodGroup().TryParse(input, out var runtimeValue));
        Assert.Equal(expected != null, ThenTryGrammars.TryParseMethodGroup(input, out var value));

        if (expected != null)
        {
            Assert.Equal(expected, runtimeValue);
            Assert.Equal(expected, value);
        }
    }

    [Fact]
    public void Conversion_Reads_The_Factory_Arguments()
    {
        Assert.True(ThenTryGrammars.TryParseReserved("foo", "bar", out var value));
        Assert.Equal("foo", value);
        Assert.False(ThenTryGrammars.TryParseReserved("bar", "bar", out _));
    }

    [Theory]
    [InlineData("  if")]
    [InlineData("  1")]
    public void Failure_Leaves_The_Cursor_Where_It_Started(string input)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();

        Assert.False(ThenTryGrammars.CreateIdentifier().Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }
}

public static partial class ThenTryGrammars
{
    public static partial bool TryParseIdentifier(string text, out string value);
    public static partial bool TryParseChoice(string text, out string value);
    public static partial bool TryParseList(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseList(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseByte(string text, out byte value);
    public static partial bool TryParseDiscarded(string text, out char value);
    public static partial bool TryParseMethodGroup(string text, out int value);
    public static partial bool TryParseReserved(string text, string reserved, out string value);

    // An identifier which isn't a reserved word, with its offsets
    public static Parser<string> CreateIdentifier() => Terms.Identifier()
        .ThenTry(static (ParseContext context, int start, int end, TextSpan name, out string result) =>
        {
            result = $"{name}@{start}-{end}";
            return !name.Span.SequenceEqual("if".AsSpan()) && !name.Span.SequenceEqual("else".AsSpan());
        });

    // The keyword an identifier rejects is the next alternative, and enough of them for a lookup table
    public static Parser<string> CreateChoice() => OneOf(
        CreateIdentifier(),
        Terms.Text("if"),
        Terms.Text("else"),
        Terms.Integer().Then(static value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Terms.Char('(').Then(static _ => "("));

    // A tuple conversion on a parser which doesn't skip white space
    public static Parser<byte> CreateByte() => Literals.Integer().ThenTry(static value => (value is >= 0 and <= 255, (byte)value));

    // A value that isn't used is still converted, and rejected
    public static Parser<char> CreateDiscarded() => Terms.Char('[').AndSkip(CreateByte()).AndSkip(Terms.Char(']'));

    // A conversion through a method group
    public static Parser<int> CreateMethodGroup() => Terms.Integer().ThenTry<int>(Narrow);

    public static (bool, int) Narrow(long value) => (value is >= int.MinValue and <= int.MaxValue, (int)value);
}
