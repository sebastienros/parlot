#nullable enable

using System.Collections.Generic;
using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// The offsets a callback receives on sequences whose first parts can match nothing, compared with the runtime in
// SequenceStartTests
public static partial class SequenceStartGrammars
{
    private static Parser<IReadOnlyList<string>> Decorators() => ZeroOrMany(Terms.Char('@').SkipAnd(Terms.Identifier().Then(static x => x.ToString()!)));

    private static Parser<Option<char>> OptionalStar() => Terms.Char('*').Optional();

    private static Parser<char> StarElse() => Terms.Char('*').Else('-');

    private static Parser<string> Class() => Terms.Text("class");

    private static Parser<string> Name() => Terms.Identifier().Then(static x => x.ToString()!);

    private static Parser<(int Start, int End)> Offsets<T>(Parser<T> parser) => parser.Then(static (_, start, end, _) => (start, end));

    [GenerateParser(nameof(TryParseZeroOrMany))]
    private static Parser<(int Start, int End)> BuildZeroOrMany() => Offsets(Decorators().And(Class()));

    [GenerateParser(nameof(TryParseOptional))]
    private static Parser<(int Start, int End)> BuildOptional() => Offsets(OptionalStar().And(Class()));

    [GenerateParser(nameof(TryParseAlways))]
    private static Parser<(int Start, int End)> BuildAlways() => Offsets(Always().And(Class()));

    [GenerateParser(nameof(TryParseElse))]
    private static Parser<(int Start, int End)> BuildElse() => Offsets(StarElse().And(Class()));

    [GenerateParser(nameof(TryParseNot))]
    private static Parser<(int Start, int End)> BuildNot() => Offsets(Not(Terms.Char('!')).And(Class()));

    [GenerateParser(nameof(TryParseZeroOrMany3))]
    private static Parser<(int Start, int End)> BuildZeroOrMany3() => Offsets(Decorators().And(Class()).And(Name()));

    [GenerateParser(nameof(TryParseOptional3))]
    private static Parser<(int Start, int End)> BuildOptional3() => Offsets(OptionalStar().And(Class()).And(Name()));

    [GenerateParser(nameof(TryParseAlways3))]
    private static Parser<(int Start, int End)> BuildAlways3() => Offsets(Always().And(Class()).And(Name()));

    [GenerateParser(nameof(TryParseElse3))]
    private static Parser<(int Start, int End)> BuildElse3() => Offsets(StarElse().And(Class()).And(Name()));

    [GenerateParser(nameof(TryParseTwoEmpty3))]
    private static Parser<(int Start, int End)> BuildTwoEmpty3() => Offsets(Decorators().And(OptionalStar()).And(Class()));

    [GenerateParser(nameof(TryParseTwoEmpty7))]
    private static Parser<(int Start, int End)> BuildTwoEmpty7() =>
        Offsets(Decorators().And(OptionalStar()).And(Always()).And(Class()).And(Name()).And(OptionalStar()).And(Name()));

    [GenerateParser(nameof(TryParseNested))]
    private static Parser<(int Start, int End)> BuildNested() => Offsets(Decorators().And(OptionalStar().And(Class())));

    [GenerateParser(nameof(TryParseSkipAnd))]
    private static Parser<(int Start, int End)> BuildSkipAnd() => Offsets(Decorators().SkipAnd(Class()));

    [GenerateParser(nameof(TryParseSkipAnd3))]
    private static Parser<(int Start, int End)> BuildSkipAnd3() => Offsets(Decorators().And(OptionalStar()).SkipAnd(Class()));

    [GenerateParser(nameof(TryParseAndSkip))]
    private static Parser<(int Start, int End)> BuildAndSkip() => Offsets(Decorators().AndSkip(Class()));

    [GenerateParser(nameof(TryParseAndSkip3))]
    private static Parser<(int Start, int End)> BuildAndSkip3() => Offsets(Decorators().And(OptionalStar()).AndSkip(Class()));

    [GenerateParser(nameof(TryParseAllEmpty))]
    private static Parser<(int Start, int End)> BuildAllEmpty() => Offsets(Decorators().And(OptionalStar()));

    [GenerateParser(nameof(TryParseAllEmpty3))]
    private static Parser<(int Start, int End)> BuildAllEmpty3() => Offsets(Decorators().And(OptionalStar()).And(Always()));

    // A literal first part keeps its start, white space included
    [GenerateParser(nameof(TryParseLiteralFirst))]
    private static Parser<(int Start, int End)> BuildLiteralFirst() => Offsets(Literals.WhiteSpace().And(Class()));

    // The value of the sequence is the one of its parts
    [GenerateParser(nameof(TryParseValues))]
    private static Parser<string> BuildValues() =>
        Decorators().And(OptionalStar()).SkipAnd(Class()).And(Name()).AndSkip(OptionalStar())
            .Then(static (_, start, end, value) => $"{start}-{end}:{value.Item2}:{value.Item3}");
}
