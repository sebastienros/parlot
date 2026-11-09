using Parlot.Fluent;
using Parlot.Analysis;
using Parlot.Rewriting;
using System.Collections.Generic;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class ThenTryTests
{
    private static readonly Parser<string> Identifier = Terms.Identifier()
        .ThenTry(static (ParseContext context, int start, int end, TextSpan name, out string result) =>
        {
            result = name.ToString();
            return result != "if";
        });

    [Fact]
    public void ShouldConvertTheValue()
    {
        Assert.True(Identifier.TryParse("  foo", out var value));
        Assert.Equal("foo", value);
    }

    [Fact]
    public void ShouldFailAndRestoreTheCursorWhenRejected()
    {
        var context = new ParseContext(new Scanner("  if"));
        var result = new ParseResult<string>();

        Assert.False(Identifier.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Position.Offset);
        Assert.Equal(1, context.Scanner.Cursor.Position.Column);
    }

    [Fact]
    public void ShouldFailAndRestoreTheCursorWhenTheParserFails()
    {
        var context = new ParseContext(new Scanner("  123"));
        var result = new ParseResult<string>();

        Assert.False(Identifier.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Position.Offset);
    }

    [Fact]
    public void ShouldReceiveTheOffsetsOfTheValue()
    {
        var parser = Terms.Identifier()
            .ThenTry(static (ParseContext context, int start, int end, TextSpan name, out (int, int) result) =>
            {
                result = (start, end);
                return true;
            });

        var context = new ParseContext(new Scanner("  foo "));
        var result = new ParseResult<(int, int)>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal((2, 5), result.Value);
        Assert.Equal(2, result.Start);
        Assert.Equal(5, result.End);
        Assert.Equal(5, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldReceiveTheContext()
    {
        var parser = Literals.Integer()
            .ThenTry(static (ParseContext context, int start, int end, long value, out long result) =>
            {
                result = value;
                return !((Flagged)context).Rejects;
            });

        var result = new ParseResult<long>();
        Assert.True(parser.Parse(new Flagged("12") { Rejects = false }, ref result));
        Assert.Equal(12, result.Value);
        Assert.False(parser.Parse(new Flagged("12") { Rejects = true }, ref result));
    }

    [Fact]
    public void TupleConversionShouldConvertOrReject()
    {
        var bytes = Literals.Integer().ThenTry(static value => (value is >= 0 and <= 255, (byte)value));

        Assert.True(bytes.TryParse("255", out var value));
        Assert.Equal((byte)255, value);

        var context = new ParseContext(new Scanner("256"));
        var result = new ParseResult<byte>();
        Assert.False(bytes.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldLetTheNextAlternativeMatch()
    {
        var parser = Identifier.Or(Terms.Text("if").Then(static _ => "keyword"));

        Assert.True(parser.TryParse(" if", out var value));
        Assert.Equal("keyword", value);

        Assert.True(parser.TryParse(" iffy", out value));
        Assert.Equal("iffy", value);
    }

    [Fact]
    public void ShouldLetTheNextAlternativeMatchInALookupTable()
    {
        // Enough alternatives for OneOf to build a table from the forwarded hints
        var parser = OneOf(
            Identifier,
            Terms.Text("if").Then(static _ => "keyword"),
            Terms.Integer().Then(static _ => "number"),
            Terms.Char('(').Then(static _ => "paren"));

        Assert.True(parser.TryParse("if", out var value));
        Assert.Equal("keyword", value);
        Assert.True(parser.TryParse("foo", out value));
        Assert.Equal("foo", value);
        Assert.True(parser.TryParse(" 1", out value));
        Assert.Equal("number", value);
    }

    [Fact]
    public void ShouldContinueALoopAfterARejection()
    {
        var parser = ZeroOrMany(Identifier).And(Terms.Text("if"));

        Assert.True(parser.TryParse("a b if", out var value));
        Assert.Equal(new[] { "a", "b" }, value.Item1);
    }

    [Fact]
    public void ShouldForwardTheHintsOfItsParser()
    {
        var seekable = Assert.IsAssignableFrom<ISeekable>(Terms.Text("abc").ThenTry(static value => (true, value)));

        Assert.True(seekable.CanSeek);
        Assert.True(seekable.SkipWhitespace);
        Assert.Equal(new[] { 'a' }, seekable.ExpectedChars);
    }

    [Fact]
    public void ShouldForwardTheHintsOfADeferredParserSetLater()
    {
        var deferred = Deferred<string>();
        var seekable = Assert.IsAssignableFrom<ISeekable>(deferred.ThenTry(static value => (true, value)));

        deferred.Parser = Terms.Text("abc");

        Assert.True(seekable.CanSeek);
        Assert.Equal(new[] { 'a' }, seekable.ExpectedChars);
    }

    [Fact]
    public void ShouldReadTheValueWhenItIsNotUsed()
    {
        var conversions = new List<string>();
        var parser = Terms.Identifier()
            .ThenTry(value =>
            {
                conversions.Add(value.ToString());
                return (value.ToString() != "if", 0);
            });

        // The skipped side of a sequence doesn't use the value
        var skipped = Terms.Char('(').AndSkip(parser);
        Assert.True(skipped.TryParse("(foo", out _));
        Assert.False(skipped.TryParse("(if", out _));
        Assert.True(parser.AndSkip(Terms.Char(';')).TryParse("bar;", out _));
        Assert.Equal(new[] { "foo", "if", "bar" }, conversions);
    }

    [Fact]
    public void LeftRecursionThroughItIsReported()
    {
        var expression = Deferred<long>();
        expression.Named("expression");
        expression.Parser = expression.ThenTry(static value => (value > 0, value)).AndSkip(Terms.Char('+')).Or(Terms.Integer());

        Assert.Contains(GrammarAnalyzer.Analyze(expression), static finding => finding.Rule == GrammarRule.LeftRecursion);

        // The left-recursive alternative never matches
        Assert.Equal(1, expression.Parse("1+2"));
    }

    private sealed class Flagged : ParseContext
    {
        public Flagged(string text) : base(new Scanner(text))
        {
        }

        public bool Rejects { get; init; }
    }
}
