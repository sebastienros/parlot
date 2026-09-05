using System;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public class ParameterizedParserTests
{
    [Fact]
    public void ParameterlessIf_UsesCurrentContextAndRetainsSingleton()
    {
        var parser = ParameterizedGrammars.ContextOnly();
        AssertGenerated(parser);
        Assert.Same(parser, ParameterizedGrammars.ContextOnly());
        var result = new ParseResult<string>();
        Assert.True(parser.Parse(new ConditionalContext("yes") { Enabled = true }, ref result));
        Assert.Equal("yes", result.Value);
        Assert.True(parser.Parse(new ConditionalContext("no") { Enabled = false }, ref result));
        Assert.Equal("no", result.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void If_UsesBoundArgumentsAndCurrentContext(int kind)
    {
        var options = new ConditionalOptions { Enabled = true };
        var parser = kind switch
        {
            0 => ParameterizedGrammars.Gate(options),
            1 => ParameterizedGrammars.Choice(options),
            2 => ParameterizedGrammars.ContextGate(options),
            3 => ParameterizedGrammars.ContextChoice(options),
            4 => ParameterizedGrammars.TypedGate(options),
            _ => ParameterizedGrammars.TypedChoice(options)
        };
        AssertGenerated(parser);
        Assert.Equal(0, options.Evaluations);

        var context = new ConditionalContext("  yes") { Enabled = true };
        var result = new ParseResult<string>();
        Assert.True(parser.Parse(context, ref result));
        Assert.Equal("yes", result.Value);
        Assert.Equal(1, options.Evaluations);

        options.Enabled = false;
        context = new ConditionalContext("  no") { Enabled = true };
        var succeeds = kind % 2 == 1;
        Assert.Equal(succeeds, parser.Parse(context, ref result));
        Assert.Equal(succeeds ? 4 : 0, context.Scanner.Cursor.Offset);
        Assert.Equal(2, options.Evaluations);

        if (kind >= 2)
        {
            options.Enabled = true;
            context = new ConditionalContext("  no") { Enabled = false };
            Assert.Equal(succeeds, parser.Parse(context, ref result));
            Assert.Equal(3, options.Evaluations);
        }
    }

    [Theory]
    [InlineData(true, " no!")]
    [InlineData(false, " yes!")]
    [InlineData(true, " yes?")]
    [InlineData(false, " no?")]
    public void SelectedBranchFailure_RestoresCursorWithoutFallback(bool enabled, string input)
    {
        var generated = ParameterizedGrammars.Backtracking(enabled);
        Func<bool, Parser<string>> factory = ParameterizedGrammars.Backtracking;
        var runtime = factory(enabled);
        AssertGenerated(generated);

        foreach (var parser in new[] { generated, runtime })
        {
            var context = new ParseContext(new Scanner(input));
            var start = context.Scanner.Cursor.Position;
            var result = new ParseResult<string>();
            Assert.False(parser.Parse(context, ref result));
            Assert.Equal(start, context.Scanner.Cursor.Position);
        }
    }

    [Fact]
    public void FactoryCalls_HaveIndependentState()
    {
        var first = ParameterizedGrammars.Multiple(3, 4, "first:");
        var second = ParameterizedGrammars.Multiple(10, 20, "second:");
        AssertGenerated(first);
        AssertGenerated(second);
        Assert.NotSame(first, second);
        Assert.Equal("first:7", first.Parse("x"));
        Assert.Equal("second:30", second.Parse("x"));
        Assert.Equal("first:7", first.Parse("x"));
    }

    [Fact]
    public void SelectAndWhen_SupportCaptures()
    {
        var parser = ParameterizedGrammars.Selected(1, "no");
        AssertGenerated(parser);
        Assert.Equal("no", parser.Parse("no"));
        Assert.Null(parser.Parse("yes"));
        Assert.Null(ParameterizedGrammars.Selected(1, "yes").Parse("no"));
        Assert.Null(ParameterizedGrammars.Selected(-1, "yes").Parse("yes"));
    }

    [Fact]
    public void SwitchAndFallbacks_SupportCaptures()
    {
        var switched = ParameterizedGrammars.Switched(1);
        AssertGenerated(switched);
        Assert.Equal("no", switched.Parse("xno"));
        Assert.Null(switched.Parse("xyes"));
        Assert.Equal("prefix:0", ParameterizedGrammars.Fallback("prefix:").Parse(""));
        Assert.Equal("prefix:x", ParameterizedGrammars.Fallback("prefix:").Parse("x"));
        Assert.Equal("prefix:x", ParameterizedGrammars.TransformedOrElse("prefix:").Parse("x"));
        Assert.Equal("fallback", ParameterizedGrammars.TransformedOrElse("prefix:").Parse(""));
    }

    [Fact]
    public void RecursiveHelpers_RetainBoundArguments()
    {
        var enabled = ParameterizedGrammars.RecursiveChoice(true);
        var disabled = ParameterizedGrammars.RecursiveChoice(false);
        AssertGenerated(enabled);
        Assert.Equal("x", enabled.Parse("((x))"));
        Assert.Null(disabled.Parse("(x)"));
        Assert.Equal("x", disabled.Parse("x"));
    }

    [Fact]
    public void Overloads_InterceptIndependently()
    {
        var parameterless = ParameterizedGrammars.Overloaded();
        var boolean = ParameterizedGrammars.Overloaded(true);
        var integer = ParameterizedGrammars.Overloaded(42);
        AssertGenerated(parameterless);
        AssertGenerated(boolean);
        AssertGenerated(integer);
        Assert.Same(parameterless, ParameterizedGrammars.Overloaded());
        Assert.Equal("default", parameterless.Parse("default"));
        Assert.Equal("yes", boolean.Parse("yes"));
        Assert.Equal("42", integer.Parse("x"));
    }

    [Fact]
    public void NamedAndOptionalArguments_AreForwarded()
    {
        Assert.Equal("default:2", ParameterizedGrammars.Optional().Parse("x"));
        Assert.Equal("named:5", ParameterizedGrammars.Optional(count: 5, prefix: "named:").Parse("x"));
        Assert.Equal("last", ParameterizedGrammars.Variadic("first", "last").Parse("x"));
    }

    [Fact]
    public void Arguments_AreEvaluatedOnceInSourceOrder()
    {
        var next = 0;
        var parser = ParameterizedGrammars.ArgumentOrder(second: ++next, first: ++next);
        AssertGenerated(parser);
        Assert.Equal(2, next);
        Assert.Equal(21, parser.Parse("x"));
        Assert.Equal(2, next);
    }

    [Fact]
    public void Captures_PreserveSymbolIdentityAndNameof()
    {
        var parser = ParameterizedGrammars.Symbols("bound", 7);
        AssertGenerated(parser);
        Assert.Equal("bound:7:context", parser.Parse("x"));
    }

    [Fact]
    public void ReferenceAndValueTypeArguments_AreNotBakedIntoGeneratedSource()
    {
        var parser = ParameterizedGrammars.Values((2, 3), new[,] { { 4 } }, null);
        AssertGenerated(parser);
        Assert.Equal(9, parser.Parse("x"));
        Assert.Equal(14, ParameterizedGrammars.Values((2, 3), new[,] { { 4 } }, 5).Parse("x"));
    }

    [Fact]
    public void NullableResults_UseRuntimeArguments()
    {
        var parser = ParameterizedGrammars.NullableValue(null);
        AssertGenerated(parser);
        var result = new ParseResult<string>();
        Assert.True(parser.Parse(new ParseContext(new Scanner("x")), ref result));
        Assert.Null(result.Value);
        Assert.Equal("value", ParameterizedGrammars.NullableValue("value").Parse("x"));
    }

    [Fact]
    public void MutableStructCaptures_PreserveClosureSemantics()
    {
        var parser = ParameterizedGrammars.Counted(default);
        AssertGenerated(parser);
        Assert.Equal(1, parser.Parse("x"));
        Assert.Equal(2, parser.Parse("x"));
    }

    [Fact]
    public void RelocatedCallbacks_PreserveEnclosingMemberBindings()
    {
        var parser = ParameterizedGrammars.EnclosingMembers("prefix:");
        AssertGenerated(parser);
        Assert.Equal("prefix:grammar:x", parser.Parse("x"));
        Assert.Equal("grammar:x", ParameterizedGrammars.EnclosingMethodGroup(0).Parse("x"));
    }

    [Fact]
    public void Captures_PreserveInferredTupleAndAnonymousPropertyNames()
    {
        var parser = ParameterizedGrammars.InferredNames("value");
        AssertGenerated(parser);
        Assert.Equal("valuevalue", parser.Parse("x"));
    }

    [Fact]
    public void Nameof_DoesNotCaptureRuntimeState()
    {
        var parser = ParameterizedGrammars.NameOnly(42);
        AssertGenerated(parser);
        Assert.Equal("value", parser.Parse("value"));
    }

    private static void AssertGenerated<T>(Parser<T> parser)
        => Assert.StartsWith("GeneratedParser_", parser.GetType().Name, StringComparison.Ordinal);
}

public sealed class ConditionalOptions
{
    private bool _enabled;

    public int Evaluations { get; private set; }

    public bool Enabled
    {
        get
        {
            Evaluations++;
            return _enabled;
        }
        set => _enabled = value;
    }
}

public sealed class ConditionalContext : ParseContext
{
    public ConditionalContext(string input) : base(new Scanner(input))
    {
    }

    public bool Enabled { get; set; }
}

public struct ConditionalCounter
{
    private int _value;

    public int Next() => ++_value;
}

public static partial class ParameterizedGrammars
{
    private const string Name = "grammar";

    private static string Parse(char value) => Name + ":" + value;

    [GenerateParser]
    public static Parser<string> ContextOnly()
        => If(static (ConditionalContext context) => context.Enabled, Literals.Text("yes"), Literals.Text("no"));

    [GenerateParser]
    public static Parser<string> Gate(ConditionalOptions options)
        => If(() => options.Enabled, Terms.Text("yes"));

    [GenerateParser]
    public static Parser<string> Choice(ConditionalOptions options)
        => If(() => options.Enabled, Terms.Text("yes"), Terms.Text("no"));

    [GenerateParser]
    public static Parser<string> ContextGate(ConditionalOptions options)
        => If(context => options.Enabled && ((ConditionalContext)context).Enabled, Terms.Text("yes"));

    [GenerateParser]
    public static Parser<string> ContextChoice(ConditionalOptions options)
        => If(context => options.Enabled && ((ConditionalContext)context).Enabled, Terms.Text("yes"), Terms.Text("no"));

    [GenerateParser]
    public static Parser<string> TypedGate(ConditionalOptions options)
        => If((ConditionalContext context) => options.Enabled && context.Enabled, Terms.Text("yes"));

    [GenerateParser]
    public static Parser<string> TypedChoice(ConditionalOptions options)
        => If((ConditionalContext context) => options.Enabled && context.Enabled, Terms.Text("yes"), Terms.Text("no"));

    [GenerateParser]
    public static Parser<string> Backtracking(bool enabled)
        => If(() => enabled, Terms.Text("yes").AndSkip(Literals.Char('!')), Terms.Text("no").AndSkip(Literals.Char('!')));

    [GenerateParser]
    public static Parser<string> Multiple(int first, int second, string prefix)
        => Literals.Char('x').Then(_ => prefix + (first + second).ToString(System.Globalization.CultureInfo.InvariantCulture));

    [GenerateParser]
    public static Parser<int> ArgumentOrder(int first, int second)
        => Literals.Char('x').Then(_ => first * 10 + second);

    [GenerateParser]
    public static Parser<string> Selected(int index, string expected)
        => Select(() => index, Literals.Text("yes"), Literals.Text("no"))
            .When((_, value) => value == expected);

    [GenerateParser]
    public static Parser<string> Switched(int index)
        => Literals.Char('x').Switch((_, value) => index, Literals.Text("yes"), Literals.Text("no"));

    [GenerateParser]
    public static Parser<string> Fallback(string prefix)
        => Literals.Text("x").Then(value => prefix + value)
            .Else(context => prefix + context.Scanner.Cursor.Offset.ToString(System.Globalization.CultureInfo.InvariantCulture));

    [GenerateParser]
    public static Parser<string> TransformedOrElse(string prefix)
        => Literals.Text("x").ThenElse(value => prefix + value, "fallback");

    [GenerateParser]
    public static Parser<string> RecursiveChoice(bool enabled)
        => Recursive<string>(self => OneOf(
            If(() => enabled, Between(Literals.Char('('), self, Literals.Char(')'))),
            Literals.Text("x")));

    [GenerateParser]
    public static Parser<string> Overloaded() => Literals.Text("default");

    [GenerateParser]
    public static Parser<string> Overloaded(bool enabled)
        => If(() => enabled, Literals.Text("yes"), Literals.Text("no"));

    [GenerateParser]
    public static Parser<string> Overloaded(int value)
        => Literals.Char('x').Then(_ => value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    [GenerateParser]
    public static Parser<string> Optional(string prefix = "default:", int count = 2)
        => Literals.Char('x').Then(_ => prefix + count.ToString(System.Globalization.CultureInfo.InvariantCulture));

    [GenerateParser]
    public static Parser<string> Variadic(params string[] values)
        => Literals.Char('x').Then(_ => values[values.Length - 1]);

    [GenerateParser]
    public static Parser<string> Symbols(string context, int @class)
        => Literals.Char('x').Then((_, value) =>
            context + ":" + @class.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + nameof(context));

    [GenerateParser]
    public static Parser<int> Values((int First, int Second) pair, int[,] values, int? optional)
        => Literals.Char('x').Then(_ => pair.First + pair.Second + values[0, 0] + (optional ?? 0));

    [GenerateParser]
    public static Parser<string> NullableValue(string value)
        => Literals.Char('x').Then(_ => value);

    [GenerateParser]
    public static Parser<int> Counted(ConditionalCounter counter)
        => Literals.Char('x').Then(_ => counter.Next());

    [GenerateParser]
    public static Parser<string> EnclosingMembers(string prefix)
        => Literals.Char('x').Then(value => prefix + Name + ":" + value);

    [GenerateParser]
    public static Parser<string> EnclosingMethodGroup(int unused)
        => Literals.Char('x').Then(Parse);

    [GenerateParser]
    public static Parser<string> InferredNames(string value)
        => Literals.Char('x').Then(_ =>
        {
            var item = new { value };
            var tuple = (value, 1);
            return item.value + tuple.value;
        });

    [GenerateParser]
    public static Parser<string> NameOnly(int value)
    {
        var probe = Literals.Char('x').Then(_ => nameof(value));
        return Literals.Text(probe.Parse("x"));
    }
}
