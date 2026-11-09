using Parlot.Fluent;
using Parlot.SourceGenerator;
using System;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class WithStateBenchmarks
{
    [GenerateParser(nameof(TryParseGenerated))]
    private static Parser<int> BuildGenerated(GeneratedFlags flags) =>
        Recursive<int>(self => OneOf(
            Between(Terms.Char('('), Separated(Terms.Char(','), self), Terms.Char(')')).Then(static list => list.Sum())
                .WithState(() => flags.Flags, f => flags.Flags = f, static f => f & ~1),
            If(
                () => (flags.Flags & 1) == 0,
                Terms.Identifier().Then(static x => x.Span.SequenceEqual("in".AsSpan()) ? 1 : 0),
                Terms.Identifier().When(static (_, x) => !x.Span.SequenceEqual("in".AsSpan())).Then(static _ => 0))))
            .Eof();
}
