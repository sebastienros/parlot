using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public static partial class Grammars
{
    [GenerateParser()]
    public static Parser<string> ParserWithNoName()
    {
        return Terms.Text("hello");
    }

    [GenerateParser("Hello")]
    public static Parser<string> HelloParser()
    {
        return Terms.Text("hello");
    }

    [GenerateParser("ParseExpression")]
    public static Parser<double> ExpressionParser()
    {
        var value = OneOf(
            Terms.Text("one").Then(_ => 1.0),
            Terms.Text("two").Then(_ => 2.0),
            Terms.Text("three").Then(_ => 3.0)
        );

        var tail = ZeroOrMany(Terms.Char('+').SkipAnd(value));

        return value.And(tail).Then(tuple =>
        {
            var (value, additions) = tuple;
            
            foreach (var v in additions)
            {
                value += v;
            }
            return value;
        });
    }
}