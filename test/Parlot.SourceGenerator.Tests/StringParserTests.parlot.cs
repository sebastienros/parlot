using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public static partial class StringGrammars
{
    [GenerateParser(nameof(TryParseDecoded))]
    private static Parser<string> BuildDecoded() =>
        SkipWhiteSpace(new StringLiteral('%')).Then(static span => span.ToString()).Eof();

    [GenerateParser(nameof(TryParseCaptured))]
    private static Parser<int> BuildCaptured() =>
        Capture(SkipWhiteSpace(new StringLiteral('%'))).Then(static span => span.Length).Eof();

    [GenerateParser(nameof(TryParseCapturedStandard))]
    private static Parser<int> BuildCapturedStandard() =>
        Capture(Literals.String()).Then(static span => span.Length).Eof();

    [GenerateParser(nameof(TryParseFallback))]
    private static Parser<string> BuildFallback() =>
        Capture(SkipWhiteSpace(new StringLiteral('%'))).AndSkip(Literals.Char('!'))
            .Then(static span => span.ToString())
            .Or(Literals.Pattern(static _ => true).Then(static span => span.ToString()))
            .Eof();

    [GenerateParser(nameof(TryParseCapturedThenDecoded))]
    private static Parser<(string, string)> BuildCapturedThenDecoded()
    {
        var text = SkipWhiteSpace(new StringLiteral('%'));
        return Capture(text).And(text)
            .Then(static pair => (pair.Item1.ToString(), pair.Item2.ToString())).Eof();
    }

    [GenerateParser(nameof(TryParseDecodedThenCaptured))]
    private static Parser<(string, string)> BuildDecodedThenCaptured()
    {
        var text = SkipWhiteSpace(new StringLiteral('%'));
        return text.And(Capture(text))
            .Then(static pair => (pair.Item1.ToString(), pair.Item2.ToString())).Eof();
    }

    [GenerateParser(nameof(TryParseCapturedThen))]
    private static Parser<int> BuildCapturedThen(StringCallbackState state) =>
        Capture(new StringLiteral('%').Then(span => state.Record(span.ToString())))
            .Then(static span => span.Length).Eof();

    [GenerateParser(nameof(TryParseCapturedThenContext))]
    private static Parser<int> BuildCapturedThenContext(StringCallbackState state) =>
        Capture(new StringLiteral('%').Then((_, span) => state.Record(span.ToString())))
            .Then(static span => span.Length).Eof();

    [GenerateParser(nameof(TryParseCapturedThenSpan))]
    private static Parser<int> BuildCapturedThenSpan(StringCallbackState state) =>
        Capture(new StringLiteral('%').Then((_, start, end, span) => state.Record(span.ToString())))
            .Then(static span => span.Length).Eof();

    [GenerateParser(nameof(TryParseCapturedWhen))]
    private static Parser<int> BuildCapturedWhen() =>
        Capture(new StringLiteral('%').When(static (_, span) => span.ToString() == "a\n"))
            .Then(static span => span.Length).Eof();

    [GenerateParser(nameof(TryParseCapturedSwitch))]
    private static Parser<int> BuildCapturedSwitch() =>
        Capture(new StringLiteral('%').Switch(static (_, span) => span.ToString() == "a\n" ? 0 : 1,
            Literals.Char('!'), Literals.Char('?')))
            .Then(static span => span.Length).Eof();

    [GenerateParser(nameof(TryParseCapturedLeftAssociative))]
    private static Parser<int> BuildCapturedLeftAssociative() =>
        Capture(new StringLiteral('%').LeftAssociative((Literals.Char('+'),
            static (left, right) => new TextSpan(StringParserCallbacks.Combine(left.ToString(), right.ToString())))))
            .Then(static span => span.Length).Eof();

    [GenerateParser(nameof(TryParseCapturedLeftAssociativeContext))]
    private static Parser<int> BuildCapturedLeftAssociativeContext() =>
        Capture(new StringLiteral('%').LeftAssociative((Literals.Char('+'),
            static (_, left, right) => new TextSpan(StringParserCallbacks.Combine(left.ToString(), right.ToString())))))
            .Then(static span => span.Length).Eof();

    [GenerateParser(nameof(TryParseCapturedUnary))]
    private static Parser<int> BuildCapturedUnary() =>
        Capture(new StringLiteral('%').Unary((Literals.Char('!'),
            static value => new TextSpan(StringParserCallbacks.Prefix(value.ToString())))))
            .Then(static span => span.Length).Eof();

    [GenerateParser(nameof(TryParseCapturedUnaryContext))]
    private static Parser<int> BuildCapturedUnaryContext() =>
        Capture(new StringLiteral('%').Unary((Literals.Char('!'),
            static (_, value) => new TextSpan(StringParserCallbacks.Prefix(value.ToString())))))
            .Then(static span => span.Length).Eof();
}
