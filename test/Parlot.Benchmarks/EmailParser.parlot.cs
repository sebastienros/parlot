using System.Collections.Generic;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public static partial class EmailParser
{
    [GenerateParser(nameof(TryParseGenerated))]
    private static Parser<string> BuildGenerated()
    {
        var dot = Literals.Char('.');
        var plus = Literals.Char('+');
        var minus = Literals.Char('-');
        var at = Literals.Char('@');
        var wordChar = Literals.Pattern(char.IsLetterOrDigit);
        var wordDotPlusMinus = OneOrMany(OneOf(wordChar.Then(static _ => 'w'), dot, plus, minus));
        var wordDotMinus = OneOrMany(OneOf(wordChar.Then(static _ => 'w'), dot, minus));
        var wordMinus = OneOrMany(OneOf(wordChar.Then(static _ => 'w'), minus));

        return Capture(wordDotPlusMinus.And(at).And(wordMinus).And(dot).And(wordDotMinus))
            .Then(static value => value.ToString());
    }
}
