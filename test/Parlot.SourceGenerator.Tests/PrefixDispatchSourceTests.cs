using System;
using System.Linq;
using Parlot.Fluent;
using Parlot.SourceGeneration;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public class PrefixDispatchSourceTests
{
    private static readonly string[] Keywords = ["if", "implicit", "in", "int", "interface", "internal", "is", "case", "catch", "char", "class"];

    private static readonly string[] Operators = ["<<=", "<=", "<<", "<>", "<", ">>=", ">=", ">>", ">", "==", "="];

    [Fact]
    public void ChoicesOfWrappedTextsDispatchOnTheirPrefixes()
    {
        AssertDispatched(OneOf(Keywords.Select(static (word, index) => Terms.Keyword(word).Then(index)).ToArray()), true);
        AssertDispatched(OneOf([.. Keywords.Select(static (word, index) => Terms.Keyword(word).Then(index)), Terms.Pattern(static c => char.IsLetter(c)).Then(-1)]), true);
        AssertDispatched(OneOf(Operators.Select(static (op, index) => Terms.Text(op).Then(index)).ToArray()), true);
        AssertDispatched(OneOf(Operators.Select(static (op, index) => Literals.Text(op).And(Literals.Char(' ')).Then(index)).ToArray()), true);
        AssertDispatched(OneOf(Keywords.Select(static (word, index) => Capture(Terms.Keyword(word)).Then(index)).ToArray()), true);

        // A wrapped choice tells the prefixes of its alternatives
        var nested = Terms.Keyword("in").Or(Terms.Keyword("int")).Or(Terms.Keyword("interface")).Then(static _ => 0);
        AssertDispatched(OneOf(nested, Terms.Keyword("if").Then(1), Terms.Keyword("is").Then(2)), true);

        var deferred = Deferred<int>();
        deferred.Parser = OneOf(Keywords.Select(static (word, index) => Terms.Keyword(word).Then(index)).ToArray());
        AssertDispatched(OneOf(deferred.Then(static value => value), Terms.Keyword("integer").Then(100), Terms.Pattern(static c => char.IsLetter(c)).Then(-1)), true);
    }

    [Fact]
    public void ChoicesWithoutLongerPrefixesKeepTheCharDispatch()
    {
        // At most two alternatives for each char
        AssertDispatched(OneOf(Terms.Keyword("if").Then(0), Terms.Keyword("in").Then(1), Terms.Keyword("else").Then(2), Terms.Keyword("while").Then(3)), false);

        // Case-insensitive texts only tell their first chars
        AssertDispatched(OneOf(Keywords.Select(static (word, index) => Terms.Keyword(word, caseInsensitive: true).Then(index)).ToArray()), false);

        // Every prefix selects every alternative
        AssertDispatched(OneOf(Terms.Text("a").Then(0), Terms.Text("a").Then(1), Terms.Text("a").Then(2)), false);
        AssertDispatched(OneOf(Terms.Text("a").Then(0), Terms.Text("a").Then(1), Terms.Pattern(static c => c != ' ').Then(static _ => 2)), false);

        // A compacting buffer may not have read past the current char
        var choice = OneOf(Keywords.Select(static (word, index) => Terms.Keyword(word).Then(index)).ToArray());
        AssertDispatched(choice, false, new SourceGenerationContext { IsCompacting = true });
    }

    [Fact]
    public void LongTextsAreDispatchedOnTheirFirstChars()
    {
        var texts = Enumerable.Range(0, 3).Select(static index => new string('a', 100) + index).ToArray();
        var source = Generate(OneOf(texts.Select(static (text, index) => Literals.Text(text).Then(index)).ToArray()), new SourceGenerationContext());

        // The texts are all the same in their first 64 chars, which only select all of them
        Assert.DoesNotContain("DispatchPrefix", source, StringComparison.Ordinal);
    }

    private static void AssertDispatched<T>(Parser<T> parser, bool expected) => AssertDispatched(parser, expected, new SourceGenerationContext());

    private static void AssertDispatched<T>(Parser<T> parser, bool expected, SourceGenerationContext context)
    {
        var source = Generate(parser, context);
        Assert.Equal(expected, source.Contains("DispatchPrefix", StringComparison.Ordinal));
    }

    private static string Generate<T>(Parser<T> parser, SourceGenerationContext context)
    {
        var source = ((ISourceable)parser).GenerateSource(context);
        return string.Join("\n", source.Body);
    }
}
