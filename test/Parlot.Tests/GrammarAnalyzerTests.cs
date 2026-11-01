using System;
using System.Collections.Generic;
using System.Linq;
using Parlot.Analysis;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// <see cref="GrammarAnalyzer"/> reports the patterns of the rules documented in docs/grammar-rules.md, and nothing on the
/// grammars which don't have them.
/// </summary>
public class GrammarAnalyzerTests
{
    private static GrammarFinding Single(IReadOnlyList<GrammarFinding> findings, GrammarRule rule)
    {
        Assert.Equal([rule], findings.Select(static finding => finding.Rule));

        return findings[0];
    }

    [Fact]
    public void RulesHaveDistinctIdsAndHelpLinks()
    {
        Assert.Equal(GrammarRule.All.Count, GrammarRule.All.Select(static rule => rule.Id).Distinct().Count());

        foreach (var rule in GrammarRule.All)
        {
            Assert.StartsWith("PARLOT1", rule.Id);
            Assert.EndsWith("#" + rule.Id.ToLowerInvariant(), rule.HelpLink);
        }
    }

    [Fact]
    public void SampleGrammarsHaveNoFindings()
    {
        Assert.Empty(GrammarAnalyzer.Analyze(Json.JsonParser.Json));
        Assert.Empty(GrammarAnalyzer.Analyze(Calc.FluentParser.Expression));
        Assert.Empty(GrammarAnalyzer.Analyze(AccessLog.LogParser.Log));
    }

    [Fact]
    public void AnalyzeRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => GrammarAnalyzer.Analyze<string>(null!));
    }

    [Fact]
    public void ChoiceMixingWhiteSpaceSkippingIsReported()
    {
        var parser = Terms.Text("a").Or(Literals.Text("b")).Or(Terms.Text("c"));

        var finding = Single(GrammarAnalyzer.Analyze(parser), GrammarRule.MixedWhiteSpaceChoice);

        Assert.Same(parser, finding.Parser);
        Assert.Contains("Terms.Text(\"a\") and Terms.Text(\"c\") skip white space and Literals.Text(\"b\") doesn't", finding.Message);
        Assert.Null(finding.Alternative);
        Assert.Equal(GrammarFixKind.SkipWhiteSpace, finding.Fix!.Kind);
        Assert.Equal([1], finding.Fix.Alternatives);
    }

    [Fact]
    public void ChoicesSkippingWhiteSpaceTheSameWayAreNotReported()
    {
        Assert.Empty(GrammarAnalyzer.Analyze(Terms.Text("a").Or(Terms.Text("b"))));
        Assert.Empty(GrammarAnalyzer.Analyze(Literals.Text("a").Or(Literals.Text("b"))));
        Assert.Empty(GrammarAnalyzer.Analyze(SkipWhiteSpace(Literals.Text("a").Or(Literals.Text("b"))).Or(Terms.Text("c"))));
    }

    [Fact]
    public void PatternAlternativeIsReported()
    {
        var parser = Terms.Text("a").Then(static x => x.Length).Or(Terms.Pattern(char.IsDigit).Then(static x => x.Length));

        var finding = Single(GrammarAnalyzer.Analyze(parser), GrammarRule.UnseekableAlternative);

        Assert.Contains("alternative 2 (Terms.Pattern(...))", finding.Message);
        Assert.Equal(1, finding.Alternative);
        Assert.Null(finding.Fix);
        Assert.Contains("Literals.Pattern(...) tests the chars with a predicate", finding.Message);
        Assert.Contains("AnyOf, NoneOf and Identifier", finding.Message);
    }

    [Fact]
    public void ScanWithoutStartCharsIsReported()
    {
        var parser = Literals.Text("a").Then(static x => x.Length).Or(Literals.Scan(static span => span.Length > 0 && span[0] == 'b' ? 1 : 0).Then(static x => x.Length));

        var finding = Single(GrammarAnalyzer.Analyze(parser), GrammarRule.UnseekableAlternative);

        Assert.Contains("pass the chars a token can start with to Scan", finding.Message);
    }

    [Fact]
    public void AlternativeStartingWithAnOptionalParserIsReported()
    {
        var parser = Literals.Text("a").Or(ZeroOrOne(Literals.Char('-')).SkipAnd(Literals.Text("b")));

        var finding = Single(GrammarAnalyzer.Analyze(parser), GrammarRule.UnseekableAlternative);

        Assert.Contains("ZeroOrOne(Literals.Char('-')), which can succeed without reading anything", finding.Message);
    }

    [Fact]
    public void AlternativeThatCantTellIfItSkipsWhiteSpaceIsReported()
    {
        var parser = Terms.Text("a").Or(ZeroOrOne(Terms.Char('-')).SkipAnd(Terms.Text("b")));

        var finding = Single(GrammarAnalyzer.Analyze(parser), GrammarRule.MixedWhiteSpaceChoice);

        Assert.Contains("ZeroOrOne(Terms.Char('-')).SkipAnd(...) doesn't, or can't tell", finding.Message);
        Assert.Empty(GrammarAnalyzer.Analyze(Terms.Text("a").Or(ZeroOrOne(Terms.Char('-')).SkipAnd(Terms.Text("b")).Lookup(true, "-b"))));
    }

    [Fact]
    public void SequenceBuiltBeforeItsDeferredParserIsNotReported()
    {
        // The sequence reads the hints of the deferred parser when the choice builds its table, so the choice can use them
        var term = Deferred<long>();
        term.Named("term");
        var parser = Literals.Text("x").Then(0L).Or(term.AndSkip(Literals.Char('!')));
        term.Parser = Literals.Integer();

        Assert.Empty(GrammarAnalyzer.Analyze(parser));

        var terms = Deferred<long>();
        terms.Named("term");
        var choice = Terms.Text("x").Then(0L).Or(terms.AndSkip(Terms.Char('!')));
        terms.Parser = Terms.Integer();

        Assert.Empty(GrammarAnalyzer.Analyze(choice));
    }

    [Fact]
    public void AlternativesTellingTheirCharsAreNotReported()
    {
        Assert.Empty(GrammarAnalyzer.Analyze(Terms.Text("a").Then(static x => x.Length).Or(Terms.AnyOf("0123456789").Then(static x => x.Length))));
        Assert.Empty(GrammarAnalyzer.Analyze(Terms.Text("a").Then(static x => x.Length).Or(Terms.Pattern(char.IsDigit).Lookup(true, "0123456789").Then(static x => x.Length))));
        Assert.Empty(GrammarAnalyzer.Analyze(Literals.Text("a").Then(static x => x.Length).Or(Literals.Scan(static span => span[0] == 'b' ? 1 : 0, "b").Then(static x => x.Length))));

        // The operator parsers tell the chars of their operand, and of their prefix operators
        var number = Terms.Integer();
        Assert.Empty(GrammarAnalyzer.Analyze(Terms.Text("x").Then(0L).Or(number.Unary((Terms.Char('-'), static x => -x)))));
        Assert.Empty(GrammarAnalyzer.Analyze(Terms.Text("x").Then(0L).Or(number.LeftAssociative((Terms.Char('+'), static (a, b) => a + b)))));
        Assert.Empty(GrammarAnalyzer.Analyze(Terms.Text("x").Then(0L).Or(number.RightAssociative((Terms.Char('^'), static (a, b) => a + b)))));
    }

    [Fact]
    public void ChoiceOfTwoTypesIsNotReportedForItsLookupTable()
    {
        // It tries its two parsers in order, without a lookup table
        Assert.Empty(GrammarAnalyzer.Analyze(Terms.Text("a").Or<string, char, object>(Literals.Char('b'))));
        Assert.Empty(GrammarAnalyzer.Analyze(Terms.Text("a").Or<string, TextSpan, object>(Terms.Pattern(static c => c == 'b'))));
    }

    [Fact]
    public void FallbackAlternativeIsNotReported()
    {
        Assert.Empty(GrammarAnalyzer.Analyze(Literals.Text("a").Or(Literals.Text("b").Else("none"))));
    }

    [Fact]
    public void AlternativesStartingWithTheSameParserAreReported()
    {
        var number = Terms.Integer().Named("number");
        var parser = number.AndSkip(Terms.Char('+')).Or(number.AndSkip(Terms.Char('-'))).Or(number);

        var finding = Single(GrammarAnalyzer.Analyze(parser), GrammarRule.SharedLeadingParser);

        Assert.Contains("alternatives 1, 2 and 3 start", finding.Message);
        Assert.Contains("start with 'number'", finding.Message);
        Assert.Equal(GrammarFixKind.FactorLeadingParser, finding.Fix!.Kind);
        Assert.Equal([0, 1, 2], finding.Fix.Alternatives);
        Assert.True(finding.Fix.PreservesBehavior);
        Assert.Equal("Parse 'number' once, then choose what follows it", finding.Fix.Title);
    }

    [Fact]
    public void AlternativesThatArentConsecutiveHaveNoFactoringFix()
    {
        var number = Terms.Integer();
        var parser = number.AndSkip(Terms.Char('+')).Or(Terms.Text("x").Then(0L)).Or(number.AndSkip(Terms.Char('-')));

        Assert.Null(Single(GrammarAnalyzer.Analyze(parser), GrammarRule.SharedLeadingParser).Fix);
    }

    [Fact]
    public void AlternativesStartingWithTheSameTextAreNotReported()
    {
        var plus = Terms.Char('+');

        Assert.Empty(GrammarAnalyzer.Analyze(plus.SkipAnd(Terms.Text("a")).Or(plus.SkipAnd(Terms.Text("b")))));
    }

    [Fact]
    public void FactoredAlternativesAreNotReported()
    {
        var number = Terms.Integer();

        Assert.Empty(GrammarAnalyzer.Analyze(number.AndSkip(Terms.Char('+').Or(Terms.Char('-')))));
    }

    [Fact]
    public void DeferredInvokingACustomParserFirstIsReported()
    {
        var rule = Deferred<string>();
        rule.Named("rule");
        rule.Parser = new Wrapper<string>(Terms.Text("a"));

        var finding = Single(GrammarAnalyzer.Analyze(rule), GrammarRule.LoopDetection);

        Assert.Contains("'rule' looks for itself", finding.Message);
        Assert.Contains("Wrapper", finding.Message);
    }

    [Fact]
    public void DeferredInvokingACustomParserAfterReadingIsNotReported()
    {
        var rule = Deferred<string>();
        rule.Parser = Terms.Char('(').SkipAnd(new Wrapper<string>(Terms.Text("a")));

        Assert.Empty(GrammarAnalyzer.Analyze(rule));
    }

    [Fact]
    public void LeftRecursionIsReported()
    {
        var expression = Deferred<long>();
        expression.Named("expression");
        expression.Parser = expression.AndSkip(Terms.Char('+')).Or(Terms.Integer());

        var finding = Single(GrammarAnalyzer.Analyze(expression), GrammarRule.LeftRecursion);

        Assert.Same(expression, finding.Parser);
        Assert.Contains("'expression' can invoke itself before reading anything ('expression' -> 'expression')", finding.Message);

        // The left-recursive alternative never matches
        Assert.Equal(1, expression.Parse("1+2"));
    }

    [Fact]
    public void IndirectLeftRecursionIsReportedOnce()
    {
        var a = Deferred<string>();
        var b = Deferred<string>();
        a.Named("a");
        b.Named("b");
        a.Parser = b.AndSkip(Terms.Char('x')).Or(Terms.Text("y"));
        b.Parser = a.AndSkip(Terms.Char('z')).Or(Terms.Text("w"));

        var finding = Single(GrammarAnalyzer.Analyze(a), GrammarRule.LeftRecursion);

        Assert.Contains("('a' -> 'b' -> 'a')", finding.Message);
    }

    [Fact]
    public void RecursionAfterReadingIsNotReported()
    {
        var expression = Deferred<long>();
        expression.Parser = Between(Terms.Char('('), expression, Terms.Char(')')).Or(Terms.Integer());

        Assert.Empty(GrammarAnalyzer.Analyze(expression));
    }

    [Fact]
    public void AlternativesAfterOneThatNeverFailsAreReported()
    {
        var parser = Terms.Text("a").Else("none").Or(Terms.Text("b")).Or(Terms.Text("c"));

        var finding = Single(GrammarAnalyzer.Analyze(parser), GrammarRule.UnreachableAlternative);

        Assert.Contains("alternative 1 (Else(Terms.Text(\"a\")))", finding.Message);
        Assert.Contains("never fails, so the alternatives after it (Terms.Text(\"b\") and Terms.Text(\"c\")) are never tried", finding.Message);
        Assert.Equal("none", parser.Parse("b"));

        Assert.Equal(0, finding.Alternative);
        Assert.Equal([1, 2, 0], finding.Fix!.Alternatives);
        Assert.Equal("Try Else(Terms.Text(\"a\")) last", finding.Fix.Title);
        Assert.DoesNotContain(GrammarAnalyzer.Analyze(Reorder(parser, finding.Fix)), static finding => finding.Rule == GrammarRule.UnreachableAlternative);
    }

    [Fact]
    public void AlternativesAfterElseErrorAreReported()
    {
        var parser = Terms.Text("a").ElseError("expected a").Or(Terms.Text("b"));

        var finding = Single(GrammarAnalyzer.Analyze(parser), GrammarRule.UnreachableAlternative);

        Assert.Contains("reports an error instead of failing", finding.Message);
        Assert.Null(finding.Fix);
        Assert.Throws<ParseException>(() => parser.Parse("b"));
    }

    [Fact]
    public void ShorterTextFirstIsReported()
    {
        var parser = Terms.Text("<").Or(Terms.Text("<="));

        var finding = Single(GrammarAnalyzer.Analyze(parser), GrammarRule.ShadowedAlternative);

        Assert.Equal("In the choice Terms.Text(\"<\").Or(Terms.Text(\"<=\")), alternative 2 (Terms.Text(\"<=\")) never matches: it only matches a text that starts with \"<\", which alternative 1 (Terms.Text(\"<\")) matches first. Try the longer alternative first, or remove the one that can't match.", finding.Message);
        Assert.Equal("<", parser.Parse("<="));

        Assert.Equal(1, finding.Alternative);
        Assert.Equal(GrammarFixKind.Reorder, finding.Fix!.Kind);
        Assert.Equal([1, 0], finding.Fix.Alternatives);
        Assert.Equal("Try Terms.Text(\"<=\") before Terms.Text(\"<\")", finding.Fix.Title);
        Assert.False(finding.Fix.PreservesBehavior);
        Assert.Empty(GrammarAnalyzer.Analyze(Reorder(parser, finding.Fix)));
    }

    [Fact]
    public void LongerTextFirstIsNotReported()
    {
        Assert.Empty(GrammarAnalyzer.Analyze(Terms.Text("<=").Or(Terms.Text("<"))));
        Assert.Empty(GrammarAnalyzer.Analyze(Terms.Keyword("in").Or(Terms.Keyword("int"))));
        Assert.DoesNotContain(GrammarAnalyzer.Analyze(Literals.Text("<").Or(Literals.Text("=")).Or(Terms.Text("<="))), static finding => finding.Rule == GrammarRule.ShadowedAlternative);
    }

    [Fact]
    public void TextBeforeKeywordIsReported()
    {
        Single(GrammarAnalyzer.Analyze(Terms.Text("in").Or(Terms.Keyword("int"))), GrammarRule.ShadowedAlternative);
        Single(GrammarAnalyzer.Analyze(Terms.Char('a').Then(static x => x.ToString()).Or(Terms.Text("ab"))), GrammarRule.ShadowedAlternative);
        Single(GrammarAnalyzer.Analyze(Terms.Text("a", caseInsensitive: true).Or(Terms.Text("AB"))), GrammarRule.ShadowedAlternative);
    }

    [Fact]
    public void SameParserFirstIsReported()
    {
        var identifier = Terms.Identifier().Named("identifier");
        var parser = identifier.Or(identifier.AndSkip(Terms.Char('(')));

        var finding = Single(GrammarAnalyzer.Analyze(parser), GrammarRule.ShadowedAlternative);

        Assert.Contains("never matches: it starts with 'identifier', which alternative 1", finding.Message);
    }

    [Fact]
    public void ShadowedOperatorIsReported()
    {
        var parser = Terms.Integer().LeftAssociative(
            (Terms.Text("<"), static (a, b) => a < b ? 1 : 0),
            (Terms.Text("<="), static (a, b) => a <= b ? 1 : 0));

        var finding = Single(GrammarAnalyzer.Analyze(parser), GrammarRule.ShadowedAlternative);

        Assert.Contains("In the operators of LeftAssociative(Terms.Number(...)), alternative 2 (Terms.Text(\"<=\")) never matches", finding.Message);

        // "<" matches, the operand after it doesn't, and the operator is rolled back
        Assert.Equal(3, parser.Parse("3 <= 5"));
    }

    [Fact]
    public void AnalysisDoesNotChangeParsing()
    {
        var expression = Deferred<long>();
        expression.Parser = Between(Terms.Char('('), expression, Terms.Char(')')).Or(Terms.Integer());

        GrammarAnalyzer.Analyze(expression);

        Assert.Equal(12, expression.Parse("((12))"));
    }

    // The choice with the alternatives of a choice in the order of a fix
    private static Parser<T> Reorder<T>(Parser<T> choice, GrammarFix fix)
    {
        var alternatives = ((OneOf<T>)choice).OriginalParsers;

        return OneOf(fix.Alternatives.Select(index => alternatives[index]).ToArray());
    }

    // A parser type the analysis can't read, which invokes another parser
    private sealed class Wrapper<T> : Parser<T>
    {
        private readonly Parser<T> _parser;

        public Wrapper(Parser<T> parser)
        {
            _parser = parser;
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result) => _parser.Parse(context, ref result);
    }
}
