using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Explorer.Sample;

public static partial class Grammars
{
    [GenerateParser(nameof(Choice), Diagnostics = true)]
    public static Parser<char> BuildChoice() =>
        Literals.Char('a').Named("a").SkipAnd(Literals.Char('b').Named("b")).Named("ab")
        .Or(Literals.Char('a').Named("a").SkipAnd(Literals.Char('c').Named("c")).Named("ac")).Named("ab | ac");

    [GenerateParser(nameof(Assignment), Diagnostics = true)]
    public static Parser<Assignment> BuildAssignment() =>
        Terms.Identifier().Named("identifier").AndSkip(Terms.Char('=').Named("equals"))
        .And(Terms.Integer().Named("integer")).AndSkip(Terms.Char(';').Named("semicolon"))
        .Then(static pair => new Assignment(pair.Item1.ToString(), checked((int)pair.Item2))).Named("assignment");

    [GenerateParser(nameof(Optional), Diagnostics = true)]
    public static Parser<char> BuildOptional() => Literals.Char('x').Named("optional x").Optional()
        .SkipAnd(Literals.Char('a').Named("a")).Named("x? a");

    [GenerateParser(nameof(Recursive), Diagnostics = true)]
    public static Parser<char> BuildRecursive()
    {
        var expression = Deferred<char>();
        expression.Named("expression");
        expression.Parser = Literals.Char('(').Named("open").SkipAnd(expression).AndSkip(Literals.Char(')').Named("close"))
            .Or(Literals.Char('a').Named("atom"));
        return expression;
    }

    [GenerateParser(nameof(Throws), Diagnostics = true)]
    public static Parser<char> BuildThrows() => Literals.Char('a').Named("a").Then<char>(static _ => throw new System.InvalidOperationException("Sample callback exception"));
}
