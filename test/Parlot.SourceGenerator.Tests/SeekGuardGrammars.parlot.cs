#nullable enable

using System;
using System.Collections.Generic;
using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Optional parsers and loops over sequences that usually don't match. The sequences read a recursive expression, so they
// are generated as helper methods rather than span code: the generated code tests the char after the white space before
// invoking them. Each has a string and a TextReader entry point, compared with the runtime parser in SeekGuardSourceTests.
[IncludeUsings("System.Collections.Generic")]
public static partial class SeekGuardGrammars
{
    // The conditional tail of an expression, and assignments in a loop: 'let' a '=' e ';' ... '}'
    [GenerateParser(nameof(TryParseStatements))]
    private static Parser<string> BuildStatements()
    {
        var expression = Deferred<string>();
        var primary = Terms.Identifier().Then(static x => x.ToString())
            .Or(Between(Terms.Char('('), expression, Terms.Char(')')));
        var tail = Terms.Char('?').SkipAnd(expression).AndSkip(Terms.Char(':')).And(expression);

        expression.Parser = primary.And(tail.Optional())
            .Then(static t => t.Item2.HasValue ? $"({t.Item1}?{t.Item2.Value.Item1}:{t.Item2.Value.Item2})" : t.Item1);

        var statement = Terms.Text("let").SkipAnd(Terms.Identifier()).AndSkip(Terms.Char('=')).And(expression).AndSkip(Terms.Char(';'))
            .Then(static t => $"{t.Item1}={t.Item2}");

        // A captured optional part keeps the white space before it
        var annotation = Capture(Terms.Char('@').And(expression)).Optional()
            .Then(static x => x.HasValue ? "[" + x.Value.ToString() + "]" : "");

        return ZeroOrMany(statement).And(annotation).And(ZeroOrOne(tail)).AndSkip(Terms.Char('}'))
            .Then(static t => string.Join(";", t.Item1) + t.Item2 + "|" + t.Item3.Count);
    }

    // A list whose separator is a sequence, and repeated tails after a first one
    [GenerateParser(nameof(TryParseLists))]
    private static Parser<string> BuildLists()
    {
        var item = Deferred<string>();
        var separator = Terms.Char(',').AndSkip(Literals.Char('|').Optional());
        var list = Between(Terms.Char('['), Separated(separator, item), Terms.Char(']'))
            .Then(static items => "[" + string.Join(",", items) + "]");
        var tail = Terms.Char('?').SkipAnd(item);

        item.Parser = Terms.Identifier().Then(static x => x.ToString()).Or(list);

        return item.And(OneOrMany(tail).Optional())
            .Then(static t => t.Item1 + (t.Item2.HasValue ? "?" + string.Join("?", t.Item2.Value) : ""));
    }
}
