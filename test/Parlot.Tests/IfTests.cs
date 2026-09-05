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

public class IfTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GuardShouldEvaluateOnceAndLeaveRejectedResultUnchanged(bool enabled)
    {
        var evaluations = 0;
        var childCalls = 0;
        var child = Terms.Text("42");
        var parser = If(() => { evaluations++; return enabled; }, child);
        var context = new ParseContext(new Scanner("!\n 42"))
        {
            OnEnterParser = (p, _) => { if (ReferenceEquals(p, child)) childCalls++; }
        };
        context.Scanner.Cursor.Advance(1);
        var start = context.Scanner.Cursor.Position;
        var result = new ParseResult<string>(7, 11, "unchanged");

        Assert.Equal(enabled, parser.Parse(context, ref result));
        Assert.Equal(1, evaluations);
        Assert.Equal(enabled ? 1 : 0, childCalls);
        Assert.Equal(enabled ? "42" : "unchanged", result.Value);
        Assert.Equal(enabled ? 3 : 7, result.Start);
        Assert.Equal(enabled ? 5 : 11, result.End);
        if (enabled)
        {
            Assert.Equal(5, context.Scanner.Cursor.Offset);
        }
        else
        {
            Assert.Equal(start, context.Scanner.Cursor.Position);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IfElseShouldExecuteOnlySelectedBranch(bool enabled)
    {
        var evaluations = 0;
        var thenCalls = 0;
        var elseCalls = 0;
        var parser = If(
            () => { evaluations++; return enabled; },
            Literals.Text("42").Then(_ => { thenCalls++; return "then"; }),
            Literals.Text("42").Then(_ => { elseCalls++; return "else"; }));

        Assert.True(parser.TryParse("42", out var result));
        Assert.Equal(enabled ? "then" : "else", result);
        Assert.Equal(enabled ? 1 : 0, thenCalls);
        Assert.Equal(enabled ? 0 : 1, elseCalls);
        Assert.Equal(1, evaluations);

        enabled = !enabled;
        Assert.True(parser.TryParse("42", out result));
        Assert.Equal(enabled ? "then" : "else", result);
        Assert.Equal(1, thenCalls);
        Assert.Equal(1, elseCalls);
        Assert.Equal(2, evaluations);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void ChildFailureShouldBacktrackWithoutFallingBack(bool enabled, bool withElse)
    {
        var evaluations = 0;
        var otherCalls = 0;
        var failing = Terms.Text("42").AndSkip(Literals.Char('!'));
        var other = Terms.Text("42").Then(value => { otherCalls++; return value; });
        Func<bool> condition = () => { evaluations++; return enabled; };
        var parser = withElse
            ? If(condition, enabled ? failing : other, enabled ? other : failing)
            : If(condition, failing);
        var context = new ParseContext(new Scanner("!\n 42?"));
        context.Scanner.Cursor.Advance(1);
        var start = context.Scanner.Cursor.Position;
        var result = new ParseResult<string>(7, 11, "unchanged");

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(start, context.Scanner.Cursor.Position);
        Assert.Equal("unchanged", result.Value);
        Assert.Equal(7, result.Start);
        Assert.Equal(11, result.End);
        Assert.Equal(1, evaluations);
        Assert.Equal(0, otherCalls);

        Assert.True(parser.Or(Terms.Text("42")).Parse(context, ref result));
        Assert.Equal("42", result.Value);
        Assert.Equal(2, evaluations);
        Assert.Equal(0, otherCalls);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ConditionShouldReceiveCurrentContext(bool typed, bool withElse)
    {
        var evaluations = 0;
        var current = new ConditionalContext("42", true);
        Func<ParseContext, bool> condition = context =>
        {
            Assert.Same(current, context);
            evaluations++;
            return current.Enabled;
        };
        Func<ConditionalContext, bool> typedCondition = context =>
        {
            Assert.Same(current, context);
            evaluations++;
            return context.Enabled;
        };
        var thenParser = Literals.Text("42");
        var elseParser = Literals.Text("17");
        var parser = (typed, withElse) switch
        {
            (true, true) => If(typedCondition, thenParser, elseParser),
            (true, false) => If(typedCondition, thenParser),
            (false, true) => If(condition, thenParser, elseParser),
            (false, false) => If(condition, thenParser)
        };

        Assert.True(parser.TryParse(current, out var value, out _));
        Assert.Equal("42", value);
        Assert.Equal(1, evaluations);

        current = new ConditionalContext("17", false);
        Assert.Equal(withElse, parser.TryParse(current, out value, out _));
        Assert.Equal(withElse ? "17" : null, value);
        Assert.Equal(withElse ? 2 : 0, current.Scanner.Cursor.Offset);
        Assert.Equal(2, evaluations);
    }

    [Theory]
    [InlineData(false, " 42", true)]
    [InlineData(true, " 42", true)]
    [InlineData(false, " x", false)]
    [InlineData(true, " x", false)]
    public void OneOfShouldNotSkipConditionOrHoistWhitespace(bool withElse, string input, bool success)
    {
        var evaluations = 0;
        Func<ParseContext, bool> condition = context =>
        {
            evaluations++;
            Assert.Equal(0, context.Scanner.Cursor.Offset);
            Assert.Equal(' ', context.Scanner.Cursor.Current);
            return true;
        };
        var conditional = withElse
            ? If(condition, Terms.Text("42"), Terms.Text("17"))
            : If(condition, Terms.Text("42"));
        Assert.False(conditional is ISeekable { CanSeek: true });
        Assert.False(conditional is ISeekable { SkipWhitespace: true });
        var parser = OneOf(conditional, Terms.Text("z")).Or(Terms.Text("y"));
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();

        Assert.Equal(success, parser.Parse(context, ref result));
        Assert.Equal(1, evaluations);
        Assert.Equal(success ? input.Length : 0, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData(true, false, "42")]
    [InlineData(false, false, "42")]
    [InlineData(true, false, "x")]
    [InlineData(true, true, "42")]
    [InlineData(false, true, "17")]
    [InlineData(true, true, "17")]
    [InlineData(false, true, "42")]
    public void ParserEntryAndExitShouldMatch(bool enabled, bool withElse, string input)
    {
        var parser = withElse
            ? If(() => enabled, Literals.Text("42"), Literals.Text("17"))
            : If(() => enabled, Literals.Text("42"));
        var stack = new Stack<object>();
        var entries = 0;
        var exits = 0;
        var context = new ParseContext(new Scanner(input))
        {
            OnEnterParser = (p, c) =>
            {
                Assert.NotNull(c);
                stack.Push(p);
                entries++;
            },
            OnExitParser = (p, _) =>
            {
                Assert.Same(p, stack.Pop());
                exits++;
            }
        };
        var result = new ParseResult<string>();

        parser.Parse(context, ref result);

        Assert.Empty(stack);
        Assert.Equal(entries, exits);
        Assert.Equal(enabled || withElse ? 2 : 1, entries);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldSupportNullableResults(bool enabled)
    {
        var reference = If(() => enabled, Always<string?>(null), Always<string?>("else"));
        var value = If(() => enabled, Always<int?>(null), Always<int?>(42));

        Assert.True(reference.TryParse("", out var referenceResult));
        Assert.Equal(enabled ? null : "else", referenceResult);
        Assert.True(value.TryParse("", out var valueResult));
        Assert.Equal(enabled ? null : (int?)42, valueResult);
        Assert.True(If(static () => true, Always<string?>(null)).TryParse("", out referenceResult));
        Assert.Null(referenceResult);
        Assert.True(If(static () => true, Always<int?>(null)).TryParse("", out valueResult));
        Assert.Null(valueResult);
    }

    [Fact]
    public void ShouldValidateArguments()
    {
        var child = Literals.Text("42");
        Assert.Throws<ArgumentNullException>("condition", () => If((Func<bool>)null!, child));
        Assert.Throws<ArgumentNullException>("condition", () => If((Func<ParseContext, bool>)null!, child));
        Assert.Throws<ArgumentNullException>("condition", () => If((Func<ConditionalContext, bool>)null!, child));
        Assert.Throws<ArgumentNullException>("condition", () => If((Func<bool>)null!, child, child));
        Assert.Throws<ArgumentNullException>("condition", () => If((Func<ParseContext, bool>)null!, child, child));
        Assert.Throws<ArgumentNullException>("condition", () => If((Func<ConditionalContext, bool>)null!, child, child));
        Assert.Throws<ArgumentNullException>("parser", () => If<string>(static () => true, null!));
        Assert.Throws<ArgumentNullException>("parser", () => If<string>(static _ => true, null!));
        Assert.Throws<ArgumentNullException>("parser", () => If<ConditionalContext, string>(static _ => true, null!));
        Assert.Throws<ArgumentNullException>("thenParser", () => If(static () => true, null!, child));
        Assert.Throws<ArgumentNullException>("thenParser", () => If(static _ => true, null!, child));
        Assert.Throws<ArgumentNullException>("thenParser", () => If<ConditionalContext, string>(static _ => true, null!, child));
        Assert.Throws<ArgumentNullException>("elseParser", () => If(static () => true, child, null!));
        Assert.Throws<ArgumentNullException>("elseParser", () => If(static _ => true, child, null!));
        Assert.Throws<ArgumentNullException>("elseParser", () => If<ConditionalContext, string>(static _ => true, child, null!));
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, false, false)]
    [InlineData(2, false, false)]
    [InlineData(0, false, true)]
    [InlineData(1, false, true)]
    [InlineData(2, false, true)]
    [InlineData(0, true, false)]
    [InlineData(1, true, false)]
    [InlineData(2, true, false)]
    [InlineData(0, true, true)]
    [InlineData(1, true, true)]
    [InlineData(2, true, true)]
    public void SourceShouldRegisterOriginalConditionAndBranchHelpers(int shape, bool withElse, bool discard)
    {
        Delegate condition = shape switch
        {
            0 => (Func<bool>)(static () => true),
            1 => (Func<ParseContext, bool>)(static context => context.Scanner.Cursor.Offset == 0),
            _ => (Func<ConditionalContext, bool>)(static context => context.Enabled)
        };
        var thenParser = Literals.Char('a');
        var elseParser = Literals.Char('b');
        var parser = (shape, withElse) switch
        {
            (0, false) => If((Func<bool>)condition, thenParser),
            (0, true) => If((Func<bool>)condition, thenParser, elseParser),
            (1, false) => If((Func<ParseContext, bool>)condition, thenParser),
            (1, true) => If((Func<ParseContext, bool>)condition, thenParser, elseParser),
            (_, false) => If((Func<ConditionalContext, bool>)condition, thenParser),
            (_, true) => If((Func<ConditionalContext, bool>)condition, thenParser, elseParser)
        };
        var context = new SourceGenerationContext("context", "Test") { DiscardResult = discard };

        var source = Assert.IsAssignableFrom<ISourceable>(parser).GenerateSource(context);

        Assert.Same(condition, Assert.Single(context.Lambdas.Enumerate()).Delegate);
        Assert.Equal(withElse ? 2 : 1, context.Helpers.Enumerate().Count());
        var call = shape == 0
            ? "_Test_lambda0()"
            : $"_Test_lambda0(({SourceGenerationContext.GetTypeName(shape == 1 ? typeof(ParseContext) : typeof(ConditionalContext))})context)";
        Assert.Equal($"if ({call})", source.Body[0]);
        Assert.Equal(withElse, source.Body.Contains("else"));
        Assert.Contains(source.Body, line => line.IndexOf($"out {(discard ? "_" : source.ValueVariable)}", StringComparison.Ordinal) >= 0);
    }

    [Fact]
    public void SourceShouldValidateContextAndBothBranches()
    {
        var supported = Literals.Text("42");
        var unsupported = new RuntimeOnlyParser();
        Assert.Throws<ArgumentNullException>("context", () => ((ISourceable)If(static () => true, supported)).GenerateSource(null!));
        Assert.Throws<NotSupportedException>(() => ((ISourceable)If(static () => true, unsupported)).GenerateSource(new()));
        Assert.Throws<NotSupportedException>(() => ((ISourceable)If(static () => false, unsupported, supported)).GenerateSource(new()));
        Assert.Throws<NotSupportedException>(() => ((ISourceable)If(static () => true, supported, unsupported)).GenerateSource(new()));
    }

    private sealed class ConditionalContext : ParseContext
    {
        public ConditionalContext(string input, bool enabled) : base(new Scanner(input))
        {
            Enabled = enabled;
        }

        public bool Enabled { get; }
    }

    private sealed class RuntimeOnlyParser : Parser<string>
    {
        public override bool Parse(ParseContext context, ref ParseResult<string> result) => throw new NotSupportedException();
    }
}
