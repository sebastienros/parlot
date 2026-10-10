using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class CursorRestoredOnFailureTests
{
    private static Parser<string> Create(string scenario, bool compiled)
    {
        var next = Literals.Text("b");
        var parser = scenario switch
        {
            "eof" => Literals.Text("ab").Eof(),
            "switch" => Literals.Text("a").Switch((_, _) => next),
            "null" => Literals.Text("a").Switch(static (_, _) => (Parser<string>)null!),
            "consume" => AnyCharBefore(Literals.Char(','), consumeDelimiter: true).Then(static x => x.ToString()),
            "keep" => AnyCharBefore(Literals.Char(',')).Then(static x => x.ToString()),
            _ => throw new System.InvalidOperationException()
        };
        return compiled ? parser.Compile() : parser;
    }

    [Theory]
    [InlineData(false, "eof", "abc")]
    [InlineData(true, "eof", "abc")]
    [InlineData(false, "switch", "ac")]
    [InlineData(true, "switch", "ac")]
    [InlineData(false, "null", "ab")]
    [InlineData(true, "null", "ab")]
    [InlineData(false, "consume", ",abc")]
    [InlineData(true, "consume", ",abc")]
    [InlineData(false, "keep", ",abc")]
    [InlineData(true, "keep", ",abc")]
    public void FailureRestoresOffsetLineAndColumn(bool compiled, string scenario, string text)
    {
        var parser = Create(scenario, compiled);
        var context = new ParseContext(new Scanner("!!" + text));
        context.Scanner.Cursor.Advance(2);
        var start = context.Scanner.Cursor.Position;
        var result = new ParseResult<string>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(start, context.Scanner.Cursor.Position);

        var alternative = parser.Or(Literals.Text(text));
        Assert.True(alternative.Parse(context, ref result));
        Assert.Equal(text, result.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulCasesStillMatch(bool compiled)
    {
        Assert.True(Create("eof", compiled).TryParse("ab", out var value));
        Assert.Equal("ab", value);
        Assert.True(Create("switch", compiled).TryParse("ab", out value));
        Assert.Equal("b", value);
        Assert.True(Create("consume", compiled).TryParse("abc,", out value));
        Assert.Equal("abc", value);
    }
    [Theory]
    [InlineData(false, "eof", "abc")]
    [InlineData(true, "eof", "abc")]
    [InlineData(false, "switch", "ac")]
    [InlineData(true, "switch", "ac")]
    [InlineData(false, "null", "ab")]
    [InlineData(true, "null", "ab")]
    [InlineData(false, "consume", ",abc")]
    [InlineData(true, "consume", ",abc")]
    public void DiscardedFailureRestoresCursor(bool compiled, string scenario, string text)
    {
        var parser = (Parser<char>)Literals.Char('!').AndSkip(Create(scenario, false));
        if (compiled) parser = parser.Compile();
        var context = new ParseContext(new Scanner("!!" + text));
        context.Scanner.Cursor.Advance();
        var start = context.Scanner.Cursor.Position;
        var result = new ParseResult<char>();
        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(start, context.Scanner.Cursor.Position);
    }

}
