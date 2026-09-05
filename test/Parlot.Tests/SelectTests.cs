#nullable enable

using Parlot.Fluent;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Linq;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class SelectTests
{
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void ContextFreeSelectorShouldRunOnceAndOnlyExecuteSelectedBranch(int index)
    {
        var evaluations = 0;
        var firstCalls = 0;
        var secondCalls = 0;
        var entries = 0;
        var exits = 0;
        var parser = Select(
            () => { evaluations++; return index; },
            Literals.Text("42").Then(_ => { firstCalls++; return "first"; }),
            Literals.Text("42").Then(_ => { secondCalls++; return "second"; }));
        var context = new ParseContext(new Scanner("!42"))
        {
            OnEnterParser = (p, _) => { if (ReferenceEquals(p, parser)) entries++; },
            OnExitParser = (p, _) => { if (ReferenceEquals(p, parser)) exits++; }
        };
        context.Scanner.Cursor.Advance(1);
        var start = context.Scanner.Cursor.Position;
        var result = new ParseResult<string>(7, 11, "unchanged");
        var success = index is 0 or 1;

        Assert.Equal(success, parser.Parse(context, ref result));
        Assert.Equal(1, evaluations);
        Assert.Equal(1, entries);
        Assert.Equal(1, exits);
        Assert.Equal(index == 0 ? 1 : 0, firstCalls);
        Assert.Equal(index == 1 ? 1 : 0, secondCalls);
        Assert.Equal(success ? (index == 0 ? "first" : "second") : "unchanged", result.Value);
        Assert.Equal(success ? 1 : 7, result.Start);
        Assert.Equal(success ? 3 : 11, result.End);
        if (!success)
        {
            Assert.Equal(start, context.Scanner.Cursor.Position);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void SelectedChildFailureShouldBacktrackWithoutFallback(int index)
    {
        var evaluations = 0;
        var otherCalls = 0;
        var failing = Terms.Text("42").AndSkip(Literals.Char('!'));
        var other = Terms.Text("42").Then(value => { otherCalls++; return value; });
        var parser = Select(() => { evaluations++; return index; }, index == 0 ? failing : other, index == 0 ? other : failing);
        var context = new ParseContext(new Scanner("!\n 42?"));
        context.Scanner.Cursor.Advance(1);
        var start = context.Scanner.Cursor.Position;
        var result = new ParseResult<string>(7, 11, "unchanged");

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(start, context.Scanner.Cursor.Position);
        Assert.Equal("unchanged", result.Value);
        Assert.Equal(1, evaluations);
        Assert.Equal(0, otherCalls);
        Assert.True(parser.Or(Terms.Text("42")).Parse(context, ref result));
        Assert.Equal("42", result.Value);
        Assert.Equal(0, otherCalls);
    }

    [Fact]
    public void ContextFreeSelectorShouldObserveUpdatedState()
    {
        var index = 0;
        var parser = Select(() => index, Literals.Text("42"), Literals.Text("17"));

        Assert.Equal("42", parser.Parse("42"));
        index = 1;
        Assert.Equal("17", parser.Parse("17"));
        Assert.False(parser.TryParse("42", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectorShouldReceiveCurrentContextOnce(bool typed)
    {
        var evaluations = 0;
        var current = new CustomContext("42", 0);
        Func<ParseContext, int> selector = context =>
        {
            Assert.Same(current, context);
            evaluations++;
            return current.Index;
        };
        Func<CustomContext, int> typedSelector = context =>
        {
            Assert.Same(current, context);
            evaluations++;
            return context.Index;
        };
        var parser = typed
            ? Select(typedSelector, Literals.Text("42"), Literals.Text("17"))
            : Select(selector, Literals.Text("42"), Literals.Text("17"));

        Assert.True(parser.TryParse(current, out var value, out _));
        Assert.Equal("42", value);
        Assert.Equal(1, evaluations);
        current = new CustomContext("17", 1);
        Assert.True(parser.TryParse(current, out value, out _));
        Assert.Equal("17", value);
        Assert.Equal(2, evaluations);
    }

    [Theory]
    [InlineData(" 42", true)]
    [InlineData(" x", false)]
    public void OneOfShouldNotSkipSelectorOrHoistWhitespace(string input, bool success)
    {
        var context = new ParseContext(new Scanner(input));
        var evaluations = 0;
        var select = Select(() =>
        {
            evaluations++;
            Assert.Equal(0, context.Scanner.Cursor.Offset);
            return 0;
        }, Terms.Text("42"), Terms.Text("17"));
        Assert.False(select is ISeekable { CanSeek: true });
        Assert.False(select is ISeekable { SkipWhitespace: true });
        var parser = OneOf(select, Terms.Text("z")).Or(Terms.Text("y"));
        var result = new ParseResult<string>();

        Assert.Equal(success, parser.Parse(context, ref result));
        Assert.Equal(1, evaluations);
        Assert.Equal(success ? input.Length : 0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ContextFreeSelectorShouldSupportNullableResults()
    {
        Assert.True(Select(static () => 0, Always<string?>(null)).TryParse("", out var reference));
        Assert.Null(reference);
        Assert.True(Select(static () => 0, Always<int?>(null)).TryParse("", out var value));
        Assert.Null(value);
    }

    [Fact]
    public void ShouldValidateArguments()
    {
        var child = Literals.Text("42");
        Assert.Throws<ArgumentNullException>("selector", () => Select((Func<int>)null!, child));
        Assert.Throws<ArgumentNullException>("selector", () => Select((Func<ParseContext, int>)null!, child));
        Assert.Throws<ArgumentNullException>("selector", () => Select((Func<CustomContext, int>)null!, child));
        Assert.Throws<ArgumentNullException>("parsers", () => Select(static () => 0, (Parser<string>[])null!));
        Assert.Throws<ArgumentNullException>("parsers", () => Select(static _ => 0, (Parser<string>[])null!));
        Assert.Throws<ArgumentNullException>("parsers", () => Select<CustomContext, string>(static _ => 0, (Parser<string>[])null!));
        Assert.Throws<ArgumentException>("parsers", () => Select(static () => 0, child, null!));
        Assert.Throws<ArgumentException>("parsers", () => Select(static _ => 0, child, null!));
        Assert.Throws<ArgumentException>("parsers", () => Select<CustomContext, string>(static _ => 0, child, null!));
        Assert.False(Select<string>(static () => 0).TryParse("42", out _));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    public void SourceShouldRegisterOriginalSelector(int shape, bool discard)
    {
        Delegate selector = shape switch
        {
            0 => (Func<int>)(static () => 0),
            1 => (Func<ParseContext, int>)(static context => context.Scanner.Cursor.Offset),
            _ => (Func<CustomContext, int>)(static context => context.Index)
        };
        var first = Literals.Char('a');
        var second = Literals.Char('b');
        var parser = shape switch
        {
            0 => Select((Func<int>)selector, first, second),
            1 => Select((Func<ParseContext, int>)selector, first, second),
            _ => Select((Func<CustomContext, int>)selector, first, second)
        };
        var context = new SourceGenerationContext("context", "Test") { DiscardResult = discard };

        var source = Assert.IsAssignableFrom<ISourceable>(parser).GenerateSource(context);

        Assert.Same(selector, Assert.Single(context.Lambdas.Enumerate()).Delegate);
        Assert.Equal(2, context.Helpers.Enumerate().Count());
        var call = shape == 0
            ? "_Test_lambda0()"
            : $"_Test_lambda0(({SourceGenerationContext.GetTypeName(shape == 1 ? typeof(ParseContext) : typeof(CustomContext))})context)";
        Assert.EndsWith($"= {call};", source.Body[0]);
        Assert.Contains(source.Body, line => line.IndexOf($"out {(discard ? "_" : source.ValueVariable)}", StringComparison.Ordinal) >= 0);
    }

    private sealed class CustomContext : ParseContext
    {
        public CustomContext(string input, int index) : base(new Scanner(input))
        {
            Index = index;
        }

        public int Index { get; }
    }
}
