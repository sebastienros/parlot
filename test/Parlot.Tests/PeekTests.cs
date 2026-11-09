using Parlot.Analysis;
using Parlot.Fluent;
using Parlot.Rewriting;
using System.IO;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class PeekTests
{
    [Fact]
    public void ShouldReturnTheValueWithoutMovingTheCursor()
    {
        var parser = Peek(Terms.Integer());
        var context = new ParseContext(new Scanner("  42 x"));
        var result = new ParseResult<long>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(42, result.Value);

        // The result is empty, where the parser was invoked
        Assert.Equal(0, result.Start);
        Assert.Equal(0, result.End);
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldRestoreTheCursorOnFailure()
    {
        // The sequence reads 'a' before it fails on 'c', and doesn't move back since Peek does
        var parser = Peek(Literals.Char('a').And(Literals.Char('b')));
        var context = new ParseContext(new Scanner("ac"));
        var result = new ParseResult<(char, char)>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        // From a later position, the cursor stays there too
        context = new ParseContext(new Scanner("xac"));
        context.Scanner.Cursor.Advance();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(1, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldLetTheNextParserReadTheSameText()
    {
        var parser = Peek(Literals.Char('x')).And(Literals.Char('x')).And(Literals.Char('y'));

        Assert.Equal(('x', 'x', 'y'), parser.Parse("xy"));
        Assert.False(parser.TryParse("y", out _));
    }

    [Fact]
    public void ShouldRestoreTheCursorAfterAStartPosition()
    {
        var parser = Terms.Identifier().And(Peek(Terms.Char('('))).And(Terms.Char('('));
        var context = new ParseContext(new Scanner("f ("));
        var result = new ParseResult<(TextSpan, char, char)>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal('(', result.Value.Item2);
        Assert.Equal(3, context.Scanner.Cursor.Offset);

        Assert.False(parser.TryParse("f x", out _));
    }

    [Fact]
    public void ShouldForwardTheHintsOfItsParser()
    {
        var peek = (ISeekable)Peek(Terms.Char('}'));

        Assert.True(peek.CanSeek);
        Assert.Equal(['}'], peek.ExpectedChars);
        Assert.True(peek.SkipWhitespace);

        // A parser that can succeed without reading has no hints
        Assert.False(((ISeekable)Peek(Literals.Char('a').Optional())).CanSeek);
    }

    [Fact]
    public void ChoicesShouldOnlyTryItForTheCharsOfItsParser()
    {
        var close = Terms.Char('}').Then(static (ParseContext context, char _) =>
        {
            ((Counter)context).Count++;
            return -1;
        });

        // An automatic semicolon: a ';', or before a '}'
        var semicolon = OneOf(Terms.Char(';').Then(static _ => 1), Peek(close));

        var context = new Counter("x");
        var result = new ParseResult<int>();
        Assert.False(semicolon.Parse(context, ref result));
        Assert.Equal(0, context.Count);
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        context = new Counter("}");
        Assert.True(semicolon.Parse(context, ref result));
        Assert.Equal(-1, result.Value);
        Assert.Equal(1, context.Count);
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        context = new Counter(";");
        Assert.True(semicolon.Parse(context, ref result));
        Assert.Equal(1, result.Value);
        Assert.Equal(0, context.Count);

    }

    [Fact]
    public void ChoicesThatSkipWhiteSpaceSkipItBeforeThePeek()
    {
        // All the alternatives skip white space, so the choice skips it once before it tries them, and the cursor stays after it
        var semicolon = OneOf(Terms.Char(';').Then(static _ => 1), Peek(Terms.Char('}')).Then(static _ => -1));
        var context = new ParseContext(new Scanner("  }"));
        var result = new ParseResult<int>();

        Assert.True(semicolon.Parse(context, ref result));
        Assert.Equal(-1, result.Value);
        Assert.Equal(2, context.Scanner.Cursor.Offset);

        Assert.True(semicolon.And(Literals.Char('}')).TryParse("  }", out var value));
        Assert.Equal((-1, '}'), value);
    }

    [Fact]
    public void ShouldStreamTheTextItReadsAgain()
    {
        var parser = ZeroOrMany(Peek(Terms.Identifier()).And(Terms.Identifier())).Eof();

        var values = parser.Parse(new StringReader("abc defghijk lmnopqrstuvwxyz a"), new StreamParseOptions { BufferSize = 4 });

        Assert.NotNull(values);
        Assert.Equal(4, values.Count);
        Assert.Equal("defghijk", values[1].Item1.ToString());
        Assert.Equal("defghijk", values[1].Item2.ToString());
        Assert.Equal("lmnopqrstuvwxyz", values[2].Item1.ToString());
    }

    [Fact]
    public void AnalyzerShouldSeeThroughIt()
    {
        // Peek(x) fails whenever x does, so it can't match after x
        var shadowed = Terms.Text("a").Or(Peek(Terms.Text("a")));
        Assert.Contains(GrammarAnalyzer.Analyze(shadowed), static finding => finding.Rule == GrammarRule.ShadowedAlternative);

        // Peek of a parser that never fails never fails either
        var unreachable = Peek(Terms.Text("a").Else("e")).Or(Terms.Text("b"));
        Assert.Contains(GrammarAnalyzer.Analyze(unreachable), static finding => finding.Rule == GrammarRule.UnreachableAlternative);

        Assert.Empty(GrammarAnalyzer.Analyze(Terms.Char(';').Then(static _ => 1).Or(Peek(Terms.Char('}')).Then(static _ => -1))));
    }

    [Fact]
    public void ShouldDetectLeftRecursionThroughIt()
    {
        var expression = Deferred<string>();
        expression.Parser = Peek(expression).AndSkip(Literals.Char('x')).Or(Literals.Text("y"));

        Assert.Contains(GrammarAnalyzer.Analyze(expression), static finding => finding.Rule == GrammarRule.LeftRecursion);
    }

    [Fact]
    public void ShouldThrowWithoutParser()
    {
        Assert.Throws<System.ArgumentNullException>(static () => Peek<string>(null!));
    }

    private sealed class Counter : ParseContext
    {
        public Counter(string text) : base(new Scanner(text))
        {
        }

        public int Count { get; set; }
    }
}
