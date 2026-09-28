using System.Collections.Generic;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class KeywordSourceBenchmarks
{
    [GenerateParser(nameof(TryParseBefore))]
    private static Parser<IReadOnlyList<KeywordSourceDeclaration>> BuildBefore() => BuildSource(baseline: true);

    [GenerateParser(nameof(TryParseAfter))]
    private static Parser<IReadOnlyList<KeywordSourceDeclaration>> BuildAfter() => BuildSource(baseline: false);

    private static Parser<IReadOnlyList<KeywordSourceDeclaration>> BuildSource(bool baseline)
    {
        var types = new Parser<string>[TypeKeywords.Length];
        for (var i = 0; i < types.Length; i++)
        {
            types[i] = baseline
                ? SkipWhiteSpace(new KeywordParserBenchmarks.BaselineKeyword(TypeKeywords[i]))
                : Terms.Keyword(TypeKeywords[i]);
        }

        var modifiers = new Parser<string>[ModifierKeywords.Length];
        for (var i = 0; i < modifiers.Length; i++)
        {
            modifiers[i] = baseline
                ? SkipWhiteSpace(new KeywordParserBenchmarks.BaselineKeyword(ModifierKeywords[i]))
                : Terms.Keyword(ModifierKeywords[i]);
        }

        var expression = Deferred<int>();
        var primary = Terms.Number<int>(NumberOptions.Integer)
            .Or(Between(Terms.Char('('), expression, Terms.Char(')')));
        var product = primary.LeftAssociative((Terms.Char('*'), static (left, right) => left * right));
        expression.Parser = product.LeftAssociative(
            (Terms.Char('+'), static (left, right) => left + right),
            (Terms.Char('-'), static (left, right) => left - right));

        var declaration = ZeroOrMany(OneOf(modifiers)).Then(static values => values.Count)
            .And(OneOf(types))
            .And(Terms.Identifier().Then(static name => name.ToString()))
            .AndSkip(Terms.Char('='))
            .And(expression)
            .AndSkip(Terms.Char(';'))
            .Then(static parts => new KeywordSourceDeclaration(parts.Item2, parts.Item3, parts.Item1, parts.Item4));

        return OneOrMany(declaration)
            .AndSkip(Literals.WhiteSpace(includeNewLines: true).Optional())
            .Eof();
    }
}
