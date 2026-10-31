#nullable enable

using System;
using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Grammars whose generated code jumps or counts on the cursor's span: TextBefore, Identifier and Pattern.
// Each has a string and a TextReader entry point, compared with the runtime parser in VectorizedTokenTests.
public static partial class VectorizedTokenGrammars
{
    // A OneOf with a parser which isn't seekable declares OtherSeekableChar: the text can't be searched
    [GenerateParser(nameof(TryParseOtherSeekableDelimiter))]
    private static Parser<string> BuildOtherSeekableDelimiter() =>
        AnyCharBefore(Literals.Text("--").Or(Literals.Pattern(static c => c == '!').Then(static x => x.ToString())))
            .Then(static span => span.ToString());

    // The delimiter skips white space before its expected chars: the text can't be searched
    [GenerateParser(nameof(TryParseWhiteSpaceDelimiter))]
    private static Parser<string> BuildWhiteSpaceDelimiter() =>
        AnyCharBefore(Terms.Text("end")).Then(static span => span.ToString());

    [GenerateParser(nameof(TryParseSeekableDelimiter))]
    private static Parser<string> BuildSeekableDelimiter() =>
        AnyCharBefore(Literals.Text("-->").Or(Literals.Text("==>"))).Then(static span => span.ToString());

    [GenerateParser(nameof(TryParseSingleCharDelimiter))]
    private static Parser<string> BuildSingleCharDelimiter() =>
        AnyCharBefore(Literals.Char(';')).Then(static span => span.ToString());

    [GenerateParser(nameof(TryParseCanBeEmpty))]
    private static Parser<string> BuildCanBeEmpty() =>
        AnyCharBefore(Literals.Text("--"), canBeEmpty: true).Then(static span => span.ToString());

    [GenerateParser(nameof(TryParseFailOnEof))]
    private static Parser<string> BuildFailOnEof() =>
        AnyCharBefore(Literals.Text("--"), failOnEof: true).Then(static span => span.ToString());

    [GenerateParser(nameof(TryParseConsumeDelimiter))]
    private static Parser<(string, int)> BuildConsumeDelimiter() =>
        AnyCharBefore(Literals.Text("--"), consumeDelimiter: true)
            .Then(static (ParseContext context, TextSpan span) => (span.ToString(), context.Scanner.Cursor.Offset));

    [GenerateParser(nameof(TryParseConsumeDelimiterFailOnEof))]
    private static Parser<string> BuildConsumeDelimiterFailOnEof() =>
        AnyCharBefore(Literals.Text("--"), failOnEof: true, consumeDelimiter: true).Then(static span => span.ToString());

    [GenerateParser(nameof(TryParseIdentifier))]
    private static Parser<string> BuildIdentifier() =>
        Literals.Identifier().Then(static span => span.ToString());

    [GenerateParser(nameof(TryParseIdentifierWithPredicates))]
    private static Parser<string> BuildIdentifierWithPredicates() =>
        Literals.Identifier(static c => c == '@', static c => c == '-').Then(static span => span.ToString());

    [GenerateParser(nameof(TryParseIdentifierWithExtraPart))]
    private static Parser<string> BuildIdentifierWithExtraPart() =>
        Literals.Identifier(extraPart: static c => c == '-').Then(static span => span.ToString());

    // A start predicate accepting the null char accepts the end of the text
    [GenerateParser(nameof(TryParseIdentifierAtEof))]
    private static Parser<(string, int)> BuildIdentifierAtEof() =>
        Literals.Identifier(static c => c == '\0')
            .Then(static (ParseContext context, TextSpan span) => (span.ToString(), context.Scanner.Cursor.Offset));

    [GenerateParser(nameof(TryParsePattern))]
    private static Parser<string> BuildPattern() =>
        Literals.Pattern(static c => char.IsDigit(c)).Then(static span => span.ToString());

    [GenerateParser(nameof(TryParseBoundedPattern))]
    private static Parser<string> BuildBoundedPattern() =>
        Literals.Pattern(static c => char.IsDigit(c), minSize: 2, maxSize: 4).Then(static span => span.ToString());

    // The padding is only tried once the bounded pattern failed with the whole text
    [GenerateParser(nameof(TryParseBoundedPatternOrPadding))]
    private static Parser<string> BuildBoundedPatternOrPadding() =>
        Literals.Pattern(static c => char.IsDigit(c), minSize: 2, maxSize: 4).Or(Literals.Pattern(static c => c == 'a', minSize: 1))
            .Then(static span => span.ToString());

    [GenerateParser(nameof(TryParseEmptyPattern))]
    private static Parser<string> BuildEmptyPattern() =>
        Literals.Pattern(static c => char.IsDigit(c), minSize: 0).Then(static span => span.ToString());

    // The line and column after a pattern with new lines
    [GenerateParser(nameof(TryParsePatternPosition))]
    private static Parser<(string, int, int, int)> BuildPatternPosition() =>
        Literals.Pattern(static c => c != ' ', maxSize: 6)
            .Then(static (ParseContext context, TextSpan span) =>
            {
                var position = context.Scanner.Cursor.Position;
                return (span.ToString(), position.Offset, position.Line, position.Column);
            });

    [GenerateParser(nameof(TryParseScanIdentifier))]
    private static Parser<string> BuildScanIdentifier() =>
        Literals.Scan(global::Parlot.Tests.LiquidScanners.Identifier, global::Parlot.Tests.LiquidScanners.StartChars).Then(static span => span.ToString());

    [GenerateParser(nameof(TryParseScanTag))]
    private static Parser<string> BuildScanTag() =>
        Terms.Text("{%")
            .SkipAnd(ZeroOrMany(Terms.Scan(global::Parlot.Tests.LiquidScanners.VariableSignature, global::Parlot.Tests.LiquidScanners.StartChars).Or(Terms.Text("|").Then(static _ => new TextSpan("|")))))
            .AndSkip(Terms.Text("-%}").Or(Terms.Text("%}")))
            .Then(static list => string.Join(",", list));

    // The scanned token is discarded
    [GenerateParser(nameof(TryParseScanDiscarded))]
    private static Parser<char> BuildScanDiscarded() =>
        Literals.Scan(global::Parlot.Tests.LiquidScanners.Identifier).SkipAnd(Literals.Char('!'));

    // The line and column after a scanned token with new lines
    [GenerateParser(nameof(TryParseScanPosition))]
    private static Parser<(string, int, int, int)> BuildScanPosition() =>
        Literals.Scan(static span => span.IndexOf(' ') is var i and >= 0 ? i : span.Length)
            .Then(static (ParseContext context, TextSpan span) =>
            {
                var position = context.Scanner.Cursor.Position;
                return (span.ToString(), position.Offset, position.Line, position.Column);
            });

    // The scanner breaks its contract by returning more chars than the span has
    [GenerateParser(nameof(TryParseScanOversize))]
    private static Parser<string> BuildScanOversize() =>
        Literals.Scan(static span => span.Length + 1).Then(static span => span.ToString());
}
