using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class InterpolatedBenchmarks
{
    // The same parsers as the runtime benchmarks, returning the number of holes
    [GenerateParser(nameof(TryParseCombinators))]
    private static Parser<int> BuildCombinators()
    {
        var expression = Deferred<int>();

        var chunk = Capture(ZeroOrMany(OneOf(
            Literals.NoneOf("`$\\"),
            Capture(Literals.Char('\\').And(Literals.Pattern(static _ => true, 1, 1))),
            Capture(Literals.Char('$').WhenNotFollowedBy(Literals.Char('{'))))));

        var substitution = Between(Literals.Text("${"), expression, Terms.Char('}')).And(chunk);

        var template = Between(Terms.Char('`'), chunk.And(ZeroOrMany(substitution)), Literals.Char('`')).Then(static x =>
        {
            var count = 0;
            foreach (var hole in x.Item2)
            {
                count += hole.Item1 + 1;
            }
            return count;
        });

        expression.Parser = OneOf(Terms.Identifier().Then(static _ => 0), template);

        return ZeroOrMany(template).Then(static x =>
        {
            var count = 0;
            foreach (var item in x)
            {
                count += item;
            }
            return count;
        }).Eof();
    }

    [GenerateParser(nameof(TryParseInterpolated))]
    private static Parser<int> BuildInterpolated()
    {
        var expression = Deferred<int>();

        var template = Terms.Interpolated("`", "`", "${", "}", expression).Then(static x =>
        {
            var count = 0;
            foreach (var hole in x.Holes)
            {
                count += hole + 1;
            }
            return count;
        });

        expression.Parser = OneOf(Terms.Identifier().Then(static _ => 0), template);

        return ZeroOrMany(template).Then(static x =>
        {
            var count = 0;
            foreach (var item in x)
            {
                count += item;
            }
            return count;
        }).Eof();
    }
}
