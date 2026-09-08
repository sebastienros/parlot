using System;
using System.Globalization;
using System.Linq;
using Parlot.Fluent;
using Parlot.SourceGeneration;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public class SourceableParsersTests
{
    [Fact]
    public void Text_Emitter_Produces_A_Direct_Cursor_Check()
    {
        var source = Generate(Literals.Text("hello"));
        var generated = string.Join(Environment.NewLine, source.Body);

        Assert.Contains("\"hello\"", generated, StringComparison.Ordinal);
        Assert.Contains("cursor", generated, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sequence_Emitter_Registers_Both_Children()
    {
        var context = new SourceGenerationContext("context", "Sequence");
        var parser = Literals.Char('a').And(Literals.Char('b'));

        var source = Assert.IsAssignableFrom<ISourceable>(parser).GenerateSource(context);

        Assert.NotEmpty(source.Body);
        Assert.True(context.Helpers.Enumerate().Count() >= 2);
    }

    [Fact]
    public void If_Emitter_Registers_The_Callback_And_Branches()
    {
        Func<bool> condition = static () => true;
        var context = new SourceGenerationContext("context", "Conditional");
        var parser = If(condition, Literals.Char('a'), Literals.Char('b'));

        var source = Assert.IsAssignableFrom<ISourceable>(parser).GenerateSource(context);

        Assert.Same(condition, Assert.Single(context.Lambdas.Enumerate()).Delegate);
        Assert.Equal(2, context.Helpers.Enumerate().Count());
        Assert.Contains(source.Body, static line => line.StartsWith("if (", StringComparison.Ordinal));
    }

    [Fact]
    public void Select_Emitter_Registers_The_Callback_And_All_Branches()
    {
        Func<int> selector = static () => 1;
        var context = new SourceGenerationContext("context", "Select");
        var parser = Select(selector, Literals.Char('a'), Literals.Char('b'));

        Assert.IsAssignableFrom<ISourceable>(parser).GenerateSource(context);

        Assert.Same(selector, Assert.Single(context.Lambdas.Enumerate()).Delegate);
        Assert.Equal(2, context.Helpers.Enumerate().Count());
    }

    [Fact]
    public void Number_Emitter_Selects_The_Target_Framework_Path()
    {
        var downlevel = new SourceGenerationContext(targetFramework: new TargetFrameworkInfo(
            TargetFrameworkIdentifier.NetCoreApp, new Version(7, 0)));
        var modern = new SourceGenerationContext(targetFramework: new TargetFrameworkInfo(
            TargetFrameworkIdentifier.NetCoreApp, new Version(8, 0)));
        var parser = new TestLongNumberLiteral();

        var downlevelSource = string.Join(Environment.NewLine, parser.GenerateSource(downlevel).Body);
        var modernSource = string.Join(Environment.NewLine, parser.GenerateSource(modern).Body);

        Assert.DoesNotContain("Numbers.TryParseNumber<", downlevelSource, StringComparison.Ordinal);
        Assert.Contains("Numbers.TryParseNumber<", modernSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Unsupported_Runtime_Parser_Is_Rejected_By_Combinator_Emitter()
    {
        var parser = If(static () => true, new RuntimeOnlyParser());

        Assert.Throws<NotSupportedException>(
            () => Assert.IsAssignableFrom<ISourceable>(parser).GenerateSource(new SourceGenerationContext()));
    }

    private static SourceResult Generate<T>(Parser<T> parser) =>
        Assert.IsAssignableFrom<ISourceable>(parser).GenerateSource(new SourceGenerationContext());

    private sealed class RuntimeOnlyParser : Parser<char>
    {
        public override bool Parse(ParseContext context, ref ParseResult<char> result) => false;
    }

    private sealed class TestLongNumberLiteral : NumberLiteralBase<long>
    {
        public override bool TryParseNumber(
            ReadOnlySpan<char> text,
            NumberStyles style,
            IFormatProvider provider,
            out long value) =>
            long.TryParse(text, style, provider, out value);
    }
}
