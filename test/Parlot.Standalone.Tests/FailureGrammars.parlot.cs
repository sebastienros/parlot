using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Standalone.Tests;

public static partial class FailureGrammars
{
    // SELECT a, b FROM t
    [GenerateParser(nameof(Select))]
    public static Parser<string> BuildSelect() =>
        Terms.Keyword("SELECT", caseInsensitive: true)
            .SkipAnd(Separated(Terms.Char(','), Terms.Identifier()))
            .AndSkip(Terms.Keyword("FROM", caseInsensitive: true))
            .And(Terms.Identifier())
            .Then(static value => value.Item2.ToString() + ":" + value.Item1.Count)
            .Eof();

    // Nested parenthesized sums: 1 + (2 + 3)
    [GenerateParser(nameof(Expression))]
    public static Parser<long> BuildExpression()
    {
        var expression = Deferred<long>();
        var primary = Terms.Integer().Or(Between(Terms.Char('('), expression, Terms.Char(')')));
        expression.Parser = primary.And(ZeroOrMany(Terms.Char('+').SkipAnd(primary)))
            .Then(static value =>
            {
                var sum = value.Item1;
                foreach (var item in value.Item2)
                {
                    sum += item;
                }
                return sum;
            });
        return expression.Eof();
    }

    // Only the overload returning the failure is declared
    [GenerateParser(nameof(Pair))]
    public static Parser<(long, long)> BuildPair() =>
        Terms.Integer().AndSkip(Terms.Char(',')).And(Terms.Integer()).Eof();

    // An Error reports its own position
    [GenerateParser(nameof(Error))]
    public static Parser<long> BuildError() =>
        Terms.Char('[').SkipAnd(Terms.Integer()).AndSkip(Terms.Char(']').ElseError("Expected ']'"));

    // Span code: the cursor doesn't move until the whole sequence matched
    [GenerateParser(nameof(Span))]
    public static Parser<string> BuildSpan() =>
        Literals.Text("abc").SkipAnd(Literals.Char(':')).SkipAnd(Literals.Pattern(static c => c == 'x', minSize: 2))
            .Then(static value => value.ToString()).Eof();

    [GenerateParser(nameof(Configured))]
    public static Parser<long> BuildConfigured(long factor) =>
        Terms.Integer().Then(value => value * factor).AndSkip(Terms.Char(';')).Eof();
}
