using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The same grammar generated with and without a recursion limit
public partial class RecursionLimitBenchmarks
{
    [GenerateParser(nameof(TryParse))]
    private static Parser<long> Build() => Grammar();

    [GenerateParser(nameof(TryParseLimited), MaxRecursionDepth = 64)]
    private static Parser<long> BuildLimited() => Grammar();

    // A recursive expression grammar: parentheses enter the Deferred parser, '-' is a prefix operator
    private static Parser<long> Grammar()
    {
        var expression = Deferred<long>();
        var primary = OneOf(Terms.Integer(), Between(Terms.Char('('), expression, Terms.Char(')')));
        var unary = primary.Unary((Terms.Text("-"), static x => -x));
        var product = unary.LeftAssociative((Terms.Text("*"), static (a, b) => a * b));
        var sum = product.LeftAssociative((Terms.Text("+"), static (a, b) => a + b), (Terms.Text("-"), static (a, b) => a - b));

        expression.Parser = sum;
        return expression;
    }
}
