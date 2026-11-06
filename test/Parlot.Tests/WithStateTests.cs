#nullable enable

using Parlot.Fluent;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class WithStateTests
{
    [Flags]
    private enum Flags
    {
        None = 0,
        Yield = 1,
        NoIn = 2,
    }

    private sealed class FlagsContext : ParseContext
    {
        public FlagsContext(string input, Flags flags = Flags.None) : base(new Scanner(input))
        {
            Flags = flags;
        }

        public Flags Flags { get; set; }
    }

    private static Parser<T> Set<T>(Parser<T> parser, Flags set, Flags clear = Flags.None)
        => parser.WithState<FlagsContext, Flags>(static c => c.Flags, static (c, f) => c.Flags = f, f => (f & ~clear) | set);

    // "yield" is a keyword when the flag is set, and an identifier otherwise
    private static readonly Parser<string> YieldOrIdentifier = If<FlagsContext, string>(
        static c => (c.Flags & Flags.Yield) != 0,
        Terms.Text("yield").Then(static _ => "keyword"),
        Terms.Identifier().Then(static x => "identifier:" + x.ToString()));

    [Fact]
    public void IfShouldReadTheStateAndItShouldBeRestoredAfterSuccess()
    {
        var parser = Set(YieldOrIdentifier, Flags.Yield);

        var context = new FlagsContext("yield");
        Assert.True(parser.TryParse(context, out var value, out _));
        Assert.Equal("keyword", value);
        Assert.Equal(Flags.None, context.Flags);

        context = new FlagsContext("yield");
        Assert.True(YieldOrIdentifier.TryParse(context, out value, out _));
        Assert.Equal("identifier:yield", value);
    }

    [Fact]
    public void ShouldClearTheState()
    {
        var parser = Set(YieldOrIdentifier, Flags.None, clear: Flags.Yield);

        var context = new FlagsContext("yield", Flags.Yield | Flags.NoIn);
        Assert.True(parser.TryParse(context, out var value, out _));
        Assert.Equal("identifier:yield", value);
        Assert.Equal(Flags.Yield | Flags.NoIn, context.Flags);
    }

    [Fact]
    public void ShouldRestoreTheStateAndTheCursorOnFailure()
    {
        // Matches "yield" then fails on the missing ';'
        var parser = Set(YieldOrIdentifier.AndSkip(Literals.Char(';')), Flags.Yield);

        var context = new FlagsContext("yield!", Flags.NoIn);
        var result = new ParseResult<string>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
        Assert.Equal(Flags.NoIn, context.Flags);
    }

    [Fact]
    public void ShouldRestoreTheStateWhenTheParserThrows()
    {
        var parser = Set(Terms.Text("yield").AndSkip(Literals.Char(';').ElseError("Expected ';'")), Flags.Yield);

        var context = new FlagsContext("yield!");
        Assert.False(parser.TryParse(context, out _, out var error));
        Assert.Equal("Expected ';'", error!.Message);
        Assert.Equal(Flags.None, context.Flags);
    }

    [Fact]
    public void ShouldNest()
    {
        var seen = new List<Flags>();
        Parser<char> Record(char c) => Terms.Char(c).When((ctx, _) =>
        {
            seen.Add(((FlagsContext)ctx).Flags);
            return true;
        });

        // a ( b ( c ) d ) e
        var parser = Record('a')
            .And(Set(Record('b').And(Set(Record('c'), Flags.NoIn)).And(Record('d')), Flags.Yield))
            .And(Record('e'));

        var context = new FlagsContext("a b c d e");
        Assert.True(parser.TryParse(context, out _, out _));
        Assert.Equal([Flags.None, Flags.Yield, Flags.Yield | Flags.NoIn, Flags.Yield, Flags.None], seen);
        Assert.Equal(Flags.None, context.Flags);
    }

    [Fact]
    public void SelectAndWhenShouldReadTheState()
    {
        var select = Select<FlagsContext, string>(static c => (c.Flags & Flags.NoIn) != 0 ? 1 : 0, Terms.Text("in"), Terms.Text("of"));
        var when = Terms.Identifier().When(static (c, x) => (((FlagsContext)c).Flags & Flags.NoIn) == 0 || !x.Span.SequenceEqual("in".AsSpan()));

        Assert.True(Set(select, Flags.NoIn).TryParse(new FlagsContext("of"), out var value, out _));
        Assert.Equal("of", value);
        Assert.False(Set(select, Flags.NoIn).TryParse(new FlagsContext("in"), out _, out _));
        Assert.True(select.TryParse(new FlagsContext("in"), out value, out _));
        Assert.Equal("in", value);

        Assert.False(Set(when, Flags.NoIn).TryParse(new FlagsContext("in"), out _, out _));
        Assert.True(Set(when, Flags.NoIn).TryParse(new FlagsContext("inner"), out _, out _));
        Assert.True(Set(when, Flags.None, clear: Flags.NoIn).TryParse(new FlagsContext("in", Flags.NoIn), out _, out _));
    }

    [Fact]
    public void ShouldUpdateAStateHeldOutsideOfTheContext()
    {
        var holder = new StrongBox<int>();
        var depth = Recursive<int>(self => OneOf(
            Between(Terms.Char('('), self, Terms.Char(')')).WithState(() => holder.Value, x => holder.Value = x, static x => x + 1),
            Terms.Char('x').Then(_ => holder.Value)));

        Assert.True(depth.TryParse("((x))", out var value));
        Assert.Equal(2, value);
        Assert.Equal(0, holder.Value);

        Assert.False(depth.TryParse("((x)", out _));
        Assert.Equal(0, holder.Value);
    }

    [Fact]
    public void ShouldForwardTheSeekHintsOfItsParser()
    {
        var deferred = Deferred<string>();
        var scoped = Set(deferred, Flags.Yield);
        var seekable = (ISeekable)scoped;

        Assert.False(seekable.CanSeek);

        deferred.Parser = Terms.Text("yield");

        Assert.True(seekable.CanSeek);
        Assert.Equal(['y'], seekable.ExpectedChars);
        Assert.True(seekable.SkipWhitespace);
    }

    [Fact]
    public void OneOfShouldDispatchOnTheCharsOfItsParser()
    {
        var scoped = Set(YieldOrIdentifier, Flags.Yield);
        var number = Terms.Integer().Then(static x => x.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var choice = OneOf(number, scoped);

        // The choice doesn't enter the scoped parser for a digit, since an identifier can't start with one
        var entered = new List<object>();
        var context = new FlagsContext(" 12") { OnEnterParser = (p, _) => entered.Add(p) };
        Assert.True(choice.TryParse(context, out var value, out _));
        Assert.Equal("12", value);
        Assert.DoesNotContain(scoped, entered);

        context = new FlagsContext(" yield") { OnEnterParser = (p, _) => entered.Add(p) };
        Assert.True(choice.TryParse(context, out value, out _));
        Assert.Equal("keyword", value);
        Assert.Contains(scoped, entered);
    }

    [Fact]
    public void ShouldValidateItsArguments()
    {
        var parser = Terms.Text("a");

        Assert.Throws<ArgumentNullException>("get", () => parser.WithState<FlagsContext, Flags>(null!, static (_, _) => { }, static f => f));
        Assert.Throws<ArgumentNullException>("set", () => parser.WithState<FlagsContext, Flags>(static c => c.Flags, null!, static f => f));
        Assert.Throws<ArgumentNullException>("update", () => parser.WithState<FlagsContext, Flags>(static c => c.Flags, static (_, _) => { }, null!));
        Assert.Throws<ArgumentNullException>("get", () => parser.WithState<int>(null!, static _ => { }, static x => x));
        Assert.Throws<ArgumentNullException>("parser", () => new WithState<ParseContext, int, string>(null!, static () => 0, static _ => { }, static x => x));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void SourceShouldSetAndRestoreTheStateInAMethodOfItsOwn(bool withContext, bool discard)
    {
        var inner = Literals.Char('a');
        var parser = (ISourceable)(withContext
            ? inner.WithState<FlagsContext, Flags>(static c => c.Flags, static (c, f) => c.Flags = f, static f => f | Flags.Yield)
            : inner.WithState(static () => 0, static _ => { }, static x => x + 1));
        var context = new SourceGenerationContext("context", "Test") { DiscardResult = discard };

        var source = parser.GenerateSource(context);

        // The parser invoking it only calls the method that holds the try/finally
        Assert.DoesNotContain("try", source.Body);
        Assert.Contains(source.Body, line => line.Contains("Test_WithState_") && line.Contains($"out {(discard ? "_" : source.ValueVariable)}"));

        var scope = Assert.Single(context.Helpers.Enumerate(), static h => h.MethodName.StartsWith("Test_WithState_", StringComparison.Ordinal) && !h.MethodName.Contains("_Parser_"));
        Assert.Contains("try", scope.Result.Body);
        Assert.Contains("finally", scope.Result.Body);
        Assert.Single(context.Helpers.Enumerate(), static h => h.MethodName.StartsWith("Test_WithState_Parser_", StringComparison.Ordinal));

        // The update, the getter and the setter
        Assert.Equal(3, context.Lambdas.Enumerate().Count());
        var typedContext = withContext ? $"(({SourceGenerationContext.GetTypeName(typeof(FlagsContext))})context)" : "";
        Assert.Contains(scope.Result.Body, line => line.EndsWith($"= _Test_lambda1({typedContext});", StringComparison.Ordinal));
        Assert.Contains(scope.Result.Body, line => line.Trim().StartsWith($"_Test_lambda2({(withContext ? typedContext + ", " : "")}savedState", StringComparison.Ordinal));
    }

    [Fact]
    public void SourceShouldRequireASourceableParser()
    {
        Assert.Throws<ArgumentNullException>("context", () => ((ISourceable)Literals.Text("a").WithState(static () => 0, static _ => { }, static x => x)).GenerateSource(null!));
        Assert.Throws<NotSupportedException>(() => ((ISourceable)new RuntimeOnlyParser().WithState(static () => 0, static _ => { }, static x => x)).GenerateSource(new()));
    }

    private sealed class StrongBox<T>
    {
        public T? Value;
    }

    private sealed class RuntimeOnlyParser : Parser<string>
    {
        public override bool Parse(ParseContext context, ref ParseResult<string> result) => throw new NotSupportedException();
    }
}
