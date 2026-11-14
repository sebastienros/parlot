#nullable enable

using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// The generated copies of the runtime grammars of LeadingWhiteSpaceParityTests: a parser which reads from where it is
// invoked, though what it reads first skips white space (a capture holds that white space, a lookahead leaves the cursor
// before it), under a When condition, a ThenTry conversion or a Then callback which receives where its value starts.
// Each value is followed by where the cursor is. Each has a string and a TextReader entry point returning the furthest
// failure.
public static partial class LeadingWhiteSpaceGrammars
{
    private static Parser<string> Located(this Parser<string> parser) =>
        parser.Then(static (context, value) => $"{value}|{context.Scanner.Cursor.Offset}");

    private static Parser<string> When(Parser<TextSpan> parser) =>
        parser.When(static (_, value) => value.Length < 3).Then(static value => value.ToString());

    private static Parser<string> Conversion(Parser<TextSpan> parser) =>
        parser.ThenTry(static (ParseContext _, int start, int end, TextSpan value, out string result) =>
        {
            result = $"{value}@{start}-{end}";
            return value.Length < 3;
        });

    private static Parser<string> Tuple(Parser<TextSpan> parser) =>
        parser.ThenTry(static value => (value.Length < 3, value.ToString()));

    private static Parser<string> Offsets(Parser<TextSpan> parser) =>
        parser.Then(static (_, start, end, value) => $"{value}@{start}-{end}");

    // A capture holds the white space its parser skips
    [GenerateParser(nameof(TryParseCaptureWhen))]
    public static Parser<string> BuildCaptureWhen() => When(Capture(Terms.Integer())).Located();

    [GenerateParser(nameof(TryParseCaptureConversion))]
    public static Parser<string> BuildCaptureConversion() => Conversion(Capture(Terms.Integer())).Located();

    [GenerateParser(nameof(TryParseCaptureTuple))]
    public static Parser<string> BuildCaptureTuple() => Tuple(Capture(Terms.Integer())).Located();

    [GenerateParser(nameof(TryParseCaptureOffsets))]
    public static Parser<string> BuildCaptureOffsets() => Offsets(Capture(Terms.Integer())).Located();

    // Where the cursor is when the value is rejected
    [GenerateParser(nameof(TryParseCaptureWhenElse))]
    public static Parser<string> BuildCaptureWhenElse() => When(Capture(Terms.Integer())).Else("none").Located();

    [GenerateParser(nameof(TryParseCaptureTupleElse))]
    public static Parser<string> BuildCaptureTupleElse() => Tuple(Capture(Terms.Integer())).Else("none").Located();

    // The capture of several tokens, and the one a wrapper invokes
    [GenerateParser(nameof(TryParseCapturedWordsWhen))]
    public static Parser<string> BuildCapturedWordsWhen() => When(Capture(Terms.Identifier().And(Terms.Integer())).Then(static value => value)).Located();

    [GenerateParser(nameof(TryParseCapturedWordsConversion))]
    public static Parser<string> BuildCapturedWordsConversion() => Conversion(Capture(Terms.Identifier().And(Terms.Integer())).Then(static value => value)).Located();

    // A lookahead leaves the cursor before the white space its parser skips
    [GenerateParser(nameof(TryParsePeekWhen))]
    public static Parser<string> BuildPeekWhen() => Peek(Terms.Char('a')).When(static (_, _) => true).SkipAnd(Literals.Text("abc")).Located();

    [GenerateParser(nameof(TryParsePeekTuple))]
    public static Parser<string> BuildPeekTuple() => Peek(Terms.Char('a')).ThenTry(static value => (true, value)).SkipAnd(Literals.Text("abc")).Located();

    [GenerateParser(nameof(TryParsePeekRejectedWhen))]
    public static Parser<string> BuildPeekRejectedWhen() => When(Peek(Terms.Identifier())).And(Terms.Identifier()).Then(static value => $"{value.Item1}{value.Item2}").Located();

    [GenerateParser(nameof(TryParsePeekRejectedConversion))]
    public static Parser<string> BuildPeekRejectedConversion() => Conversion(Peek(Terms.Identifier())).And(Terms.Identifier()).Then(static value => $"{value.Item1}{value.Item2}").Located();

    [GenerateParser(nameof(TryParsePeekOffsets))]
    public static Parser<string> BuildPeekOffsets() => Offsets(Peek(Terms.Identifier())).And(Terms.Identifier()).Then(static value => $"{value.Item1}{value.Item2}").Located();

    // A capture after a part of a sequence which matched nothing: the sequence starts where the capture does
    [GenerateParser(nameof(TryParseSequenceOffsets))]
    public static Parser<string> BuildSequenceOffsets() =>
        Peek(Terms.Identifier()).And(Capture(Terms.Identifier())).Then(static (_, start, end, value) => $"{value.Item2}@{start}-{end}").Located();

    [GenerateParser(nameof(TryParseOptionalCaptureOffsets))]
    public static Parser<string> BuildOptionalCaptureOffsets() =>
        Capture(Terms.Identifier()).Optional().And(Terms.Integer()).Then(static (_, start, end, value) => $"{value.Item1.OrSome(default)}{value.Item2}@{start}-{end}").Located();

    // The parsers which skip the white space first themselves: it is skipped for them, as before
    [GenerateParser(nameof(TryParseTokenWhen))]
    public static Parser<string> BuildTokenWhen() => When(Terms.Identifier()).Located();

    [GenerateParser(nameof(TryParseTokenConversion))]
    public static Parser<string> BuildTokenConversion() => Conversion(Terms.Identifier()).Located();

    [GenerateParser(nameof(TryParseTokenOffsets))]
    public static Parser<string> BuildTokenOffsets() => Offsets(Terms.Identifier()).Located();

    [GenerateParser(nameof(TryParseWordsConversion))]
    public static Parser<string> BuildWordsConversion() => Conversion(Terms.Identifier().AndSkip(Terms.Char(';'))).Located();

    [GenerateParser(nameof(TryParseWordsOffsets))]
    public static Parser<string> BuildWordsOffsets() => Offsets(Terms.Identifier().AndSkip(Terms.Char(';'))).Located();

    // A choice skips the white space for its alternatives, captures included
    [GenerateParser(nameof(TryParseChoiceWhen))]
    public static Parser<string> BuildChoiceWhen() => When(OneOf(Capture(Terms.Integer()), Capture(Terms.Identifier()))).Located();

    [GenerateParser(nameof(TryParseChoiceConversion))]
    public static Parser<string> BuildChoiceConversion() => Conversion(OneOf(Capture(Terms.Integer()), Capture(Terms.Identifier()))).Located();

    [GenerateParser(nameof(TryParseChoiceOffsets))]
    public static Parser<string> BuildChoiceOffsets() => Offsets(OneOf(Capture(Terms.Integer()), Capture(Terms.Identifier()))).Located();

    // Comments are white space too
    [GenerateParser(nameof(TryParseCommentsWhen))]
    public static Parser<string> BuildCommentsWhen() =>
        Capture(Terms.Integer()).When(static (_, value) => value.Length < 9).Then(static value => value.ToString()).Located()
            .WithComments(static comments => comments.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"));

    [GenerateParser(nameof(TryParseCommentsConversion))]
    public static Parser<string> BuildCommentsConversion() =>
        Capture(Terms.Integer()).ThenTry(static (ParseContext _, int start, int end, TextSpan value, out string result) =>
        {
            result = $"{value}@{start}-{end}";
            return value.Length < 9;
        }).Located().WithComments(static comments => comments.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"));
}
