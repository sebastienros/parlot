using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The generated grammar of ExpressionTableBenchmarks, built like ExpressionTableGrammar
public partial class ExpressionTableBenchmarks
{
    [GenerateParser(nameof(TryParse))]
    private static Parser<ExpressionNode> Build()
    {
        var expression = Deferred<ExpressionNode>();
        // The number of arguments of a call, which can have none
        var arguments = Between(Terms.Char('('), Separated(Terms.Char(','), expression).Optional(), Terms.Char(')'))
            .Then(static x => x.HasValue ? x.Value.Count : 0);
        var primary = OneOf(
            Terms.Integer().Then(static x => new ExpressionNode("number", value: x)),
            Terms.Identifier().And(arguments.Optional()).Then(static x => x.Item2.HasValue
                ? new ExpressionNode("call", value: x.Item1.Length + x.Item2.Value)
                : new ExpressionNode("name", value: x.Item1.Length)),
            Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Postfix((Terms.Char('\''), static x => new ExpressionNode("'", x)))
            .RightAssociative((Terms.Text("**"), static (a, b) => new ExpressionNode("**", a, b)))
            .Unary(
                (Terms.Text("!"), static x => new ExpressionNode("!", x)),
                (Terms.Text("-"), static x => new ExpressionNode("neg", x)),
                (Terms.Text("~"), static x => new ExpressionNode("~", x)))
            .LeftAssociative(
                (Terms.Text("*"), static (a, b) => new ExpressionNode("*", a, b)),
                (Terms.Text("/"), static (a, b) => new ExpressionNode("/", a, b)),
                (Terms.Text("%"), static (a, b) => new ExpressionNode("%", a, b)))
            .LeftAssociative(
                (Terms.Text("+"), static (a, b) => new ExpressionNode("+", a, b)),
                (Terms.Text("-"), static (a, b) => new ExpressionNode("-", a, b)))
            .LeftAssociative(
                (Terms.Text("<<"), static (a, b) => new ExpressionNode("<<", a, b)),
                (Terms.Text(">>"), static (a, b) => new ExpressionNode(">>", a, b)))
            .LeftAssociative(
                (Terms.Text("<="), static (a, b) => new ExpressionNode("<=", a, b)),
                (Terms.Text(">="), static (a, b) => new ExpressionNode(">=", a, b)),
                (Terms.Text("<"), static (a, b) => new ExpressionNode("<", a, b)),
                (Terms.Text(">"), static (a, b) => new ExpressionNode(">", a, b)))
            .LeftAssociative(
                (Terms.Text("=="), static (a, b) => new ExpressionNode("==", a, b)),
                (Terms.Text("!="), static (a, b) => new ExpressionNode("!=", a, b)))
            .LeftAssociative((Terms.Text("^"), static (a, b) => new ExpressionNode("^", a, b)))
            .LeftAssociative((Terms.Text("&&"), static (a, b) => new ExpressionNode("&&", a, b)))
            .LeftAssociative((Terms.Text("||"), static (a, b) => new ExpressionNode("||", a, b)))
            .RightAssociative((Terms.Text("??"), static (a, b) => new ExpressionNode("??", a, b)));

        return expression.Eof();
    }
}
