using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;

namespace Parlot.Fluent;

public sealed class SequenceSkipAnd<T1, T2> : Parser<T2>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable, ISequenceSourceable
{
    private readonly Parser<T1> _parser1;
    private readonly Parser<T2> _parser2;

    // Whether the parsers are invoked without their value when it isn't used
    private readonly bool _withoutValue1;
    private readonly bool _withoutValue2;

    public SequenceSkipAnd(Parser<T1> parser1, Parser<T2> parser2)
    {
        _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
        _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
        _withoutValue1 = _parser1.CanParseWithoutValue;
        _withoutValue2 = _parser2.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, the second one when the first is a lookahead, read when they are asked
    // for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => Hints.CanSeek;

    public char[] ExpectedChars => Hints.ExpectedChars;

    public bool SkipWhitespace => Hints.SkipWhitespace;

    private ForwardedHints Hints => ForwardedHints.OfSequence(ref _hints, _parser1, _parser2);

    string[]? ISeekablePrefixes.ExpectedPrefixes => ForwardedHints.SequencePrefixes(ref _hints, _parser1, _parser2);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser1) && analysis.Invoke(_parser2);

    public override bool Parse(ParseContext context, ref ParseResult<T2> result)
    {
        context.EnterParser(this);

        var parseResult1 = new ParseResult<T1>();

        var start = context.Scanner.Cursor.Offset;

        if (_withoutValue1 ? _parser1.ParseWithoutValue(context, ref parseResult1) : _parser1.Parse(context, ref parseResult1))
        {
            var parseResult2 = new ParseResult<T2>();

            if (_parser2.Parse(context, ref parseResult2))
            {
                SequenceStart.Set(ref result, in parseResult1, in parseResult2, parseResult2.Value);

                context.ExitParser(this);
                return true;
            }

            context.Scanner.Cursor.ResetPosition(start);
        }

        context.ExitParser(this);
        return false;
    }

    internal override bool CanParseWithoutValue => _withoutValue1 || _withoutValue2;

    // Parse, without the tuple
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<T2> result)
    {
        context.EnterParser(this);

        var parseResult1 = new ParseResult<T1>();

        var start = context.Scanner.Cursor.Offset;

        if (_withoutValue1 ? _parser1.ParseWithoutValue(context, ref parseResult1) : _parser1.Parse(context, ref parseResult1))
        {
            var parseResult2 = new ParseResult<T2>();

            if (_withoutValue2 ? _parser2.ParseWithoutValue(context, ref parseResult2) : _parser2.Parse(context, ref parseResult2))
            {
                SequenceStart.SetRange(ref result, in parseResult1, in parseResult2);

                context.ExitParser(this);
                return true;
            }

            context.Scanner.Cursor.ResetPosition(start);
        }

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
        => SequenceSource.Generate(this, typeof(T2), "SequenceSkipAnd", context);

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser1, _parser2);

    IReadOnlyList<(object Parser, Type ValueType, bool Keep)> ISequenceSourceable.Parts => [(_parser1, typeof(T1), false), (_parser2, typeof(T2), true)];

    string ISequenceSourceable.CreateValue(string?[] values) => values[1]!;

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail, this);

    public override string ToString() => FormatOnce($"{_parser1} & {_parser2} (skip)");
}

public sealed class SequenceSkipAnd<T1, T2, T3> : Parser<ValueTuple<T1, T3>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable, ISequenceSourceable
{
    private readonly Parser<ValueTuple<T1, T2>> _parser;
    private readonly Parser<T3> _lastParser;

    // Whether the parsers are invoked without their value when it isn't used
    private readonly bool _parserWithoutValue;
    private readonly bool _lastParserWithoutValue;

    public SequenceSkipAnd(Parser<ValueTuple<T1, T2>>
        parser,
        Parser<T3> lastParser
        )
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        _parserWithoutValue = _parser.CanParseWithoutValue;
        _lastParserWithoutValue = _lastParser.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser) && analysis.Invoke(_lastParser);

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T3>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T3>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    lastResult.Value
                );

                SequenceStart.Set(ref result, in tupleResult, in lastResult, tuple);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    internal override bool CanParseWithoutValue => _parserWithoutValue || _lastParserWithoutValue;

    // Parse, without the tuple
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<ValueTuple<T1, T3>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parserWithoutValue ? _parser.ParseWithoutValue(context, ref tupleResult) : _parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T3>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                SequenceStart.SetRange(ref result, in tupleResult, in lastResult);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
        => SequenceSource.Generate(this, typeof(ValueTuple<T1, T3>), "SequenceSkipAnd", context);

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser, _lastParser);

    IReadOnlyList<(object Parser, Type ValueType, bool Keep)> ISequenceSourceable.Parts => [(_parser, typeof(ValueTuple<T1, T2>), true), (_lastParser, typeof(T3), true)];

    string ISequenceSourceable.CreateValue(string?[] values) => $"new {SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T3>))}({values[0]}.Item1, {values[1]})";

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail, this);

    public override string ToString() => FormatOnce($"{_parser} & {_lastParser} (skip)");
}

public sealed class SequenceSkipAnd<T1, T2, T3, T4> : Parser<ValueTuple<T1, T2, T4>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable, ISequenceSourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3>> _parser;
    private readonly Parser<T4> _lastParser;

    // Whether the parsers are invoked without their value when it isn't used
    private readonly bool _parserWithoutValue;
    private readonly bool _lastParserWithoutValue;

    public SequenceSkipAnd(Parser<ValueTuple<T1, T2, T3>> parser, Parser<T4> lastParser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        _parserWithoutValue = _parser.CanParseWithoutValue;
        _lastParserWithoutValue = _lastParser.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser) && analysis.Invoke(_lastParser);

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T4>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T4>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    lastResult.Value
                );

                SequenceStart.Set(ref result, in tupleResult, in lastResult, tuple);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    internal override bool CanParseWithoutValue => _parserWithoutValue || _lastParserWithoutValue;

    // Parse, without the tuple
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T4>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parserWithoutValue ? _parser.ParseWithoutValue(context, ref tupleResult) : _parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T4>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                SequenceStart.SetRange(ref result, in tupleResult, in lastResult);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
        => SequenceSource.Generate(this, typeof(ValueTuple<T1, T2, T4>), "SequenceSkipAnd", context);

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser, _lastParser);

    IReadOnlyList<(object Parser, Type ValueType, bool Keep)> ISequenceSourceable.Parts => [(_parser, typeof(ValueTuple<T1, T2, T3>), true), (_lastParser, typeof(T4), true)];

    string ISequenceSourceable.CreateValue(string?[] values) => $"new {SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T4>))}({values[0]}.Item1, {values[0]}.Item2, {values[1]})";

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail, this);

    public override string ToString() => FormatOnce($"{_parser} & {_lastParser} (skip)");

}

public sealed class SequenceSkipAnd<T1, T2, T3, T4, T5> : Parser<ValueTuple<T1, T2, T3, T5>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable, ISequenceSourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4>> _parser;
    private readonly Parser<T5> _lastParser;

    // Whether the parsers are invoked without their value when it isn't used
    private readonly bool _parserWithoutValue;
    private readonly bool _lastParserWithoutValue;

    public SequenceSkipAnd(Parser<ValueTuple<T1, T2, T3, T4>> parser, Parser<T5> lastParser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        _parserWithoutValue = _parser.CanParseWithoutValue;
        _lastParserWithoutValue = _lastParser.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser) && analysis.Invoke(_lastParser);

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T5>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T5>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    lastResult.Value
                );

                SequenceStart.Set(ref result, in tupleResult, in lastResult, tuple);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    internal override bool CanParseWithoutValue => _parserWithoutValue || _lastParserWithoutValue;

    // Parse, without the tuple
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T5>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parserWithoutValue ? _parser.ParseWithoutValue(context, ref tupleResult) : _parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T5>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                SequenceStart.SetRange(ref result, in tupleResult, in lastResult);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
        => SequenceSource.Generate(this, typeof(ValueTuple<T1, T2, T3, T5>), "SequenceSkipAnd", context);

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser, _lastParser);

    IReadOnlyList<(object Parser, Type ValueType, bool Keep)> ISequenceSourceable.Parts => [(_parser, typeof(ValueTuple<T1, T2, T3, T4>), true), (_lastParser, typeof(T5), true)];

    string ISequenceSourceable.CreateValue(string?[] values) => $"new {SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T5>))}({values[0]}.Item1, {values[0]}.Item2, {values[0]}.Item3, {values[1]})";

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail, this);

    public override string ToString() => FormatOnce($"{_parser} & {_lastParser} (skip)");

}

public sealed class SequenceSkipAnd<T1, T2, T3, T4, T5, T6> : Parser<ValueTuple<T1, T2, T3, T4, T6>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable, ISequenceSourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4, T5>> _parser;
    private readonly Parser<T6> _lastParser;

    // Whether the parsers are invoked without their value when it isn't used
    private readonly bool _parserWithoutValue;
    private readonly bool _lastParserWithoutValue;

    public SequenceSkipAnd(Parser<ValueTuple<T1, T2, T3, T4, T5>> parser, Parser<T6> lastParser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        _parserWithoutValue = _parser.CanParseWithoutValue;
        _lastParserWithoutValue = _lastParser.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser) && analysis.Invoke(_lastParser);

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T6>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T6>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    lastResult.Value
                );

                SequenceStart.Set(ref result, in tupleResult, in lastResult, tuple);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    internal override bool CanParseWithoutValue => _parserWithoutValue || _lastParserWithoutValue;

    // Parse, without the tuple
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T6>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parserWithoutValue ? _parser.ParseWithoutValue(context, ref tupleResult) : _parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T6>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                SequenceStart.SetRange(ref result, in tupleResult, in lastResult);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
        => SequenceSource.Generate(this, typeof(ValueTuple<T1, T2, T3, T4, T6>), "SequenceSkipAnd", context);

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser, _lastParser);

    IReadOnlyList<(object Parser, Type ValueType, bool Keep)> ISequenceSourceable.Parts => [(_parser, typeof(ValueTuple<T1, T2, T3, T4, T5>), true), (_lastParser, typeof(T6), true)];

    string ISequenceSourceable.CreateValue(string?[] values) => $"new {SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T4, T6>))}({values[0]}.Item1, {values[0]}.Item2, {values[0]}.Item3, {values[0]}.Item4, {values[1]})";

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail, this);

    public override string ToString() => FormatOnce($"{_parser} & {_lastParser} (skip)");

}

public sealed class SequenceSkipAnd<T1, T2, T3, T4, T5, T6, T7> : Parser<ValueTuple<T1, T2, T3, T4, T5, T7>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable, ISequenceSourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> _parser;
    private readonly Parser<T7> _lastParser;

    // Whether the parsers are invoked without their value when it isn't used
    private readonly bool _parserWithoutValue;
    private readonly bool _lastParserWithoutValue;

    public SequenceSkipAnd(Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> parser, Parser<T7> lastParser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        _parserWithoutValue = _parser.CanParseWithoutValue;
        _lastParserWithoutValue = _lastParser.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser) && analysis.Invoke(_lastParser);

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T7>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T7>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5,
                    lastResult.Value
                );

                SequenceStart.Set(ref result, in tupleResult, in lastResult, tuple);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    internal override bool CanParseWithoutValue => _parserWithoutValue || _lastParserWithoutValue;

    // Parse, without the tuple
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T7>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parserWithoutValue ? _parser.ParseWithoutValue(context, ref tupleResult) : _parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T7>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                SequenceStart.SetRange(ref result, in tupleResult, in lastResult);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
        => SequenceSource.Generate(this, typeof(ValueTuple<T1, T2, T3, T4, T5, T7>), "SequenceSkipAnd", context);

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser, _lastParser);

    IReadOnlyList<(object Parser, Type ValueType, bool Keep)> ISequenceSourceable.Parts => [(_parser, typeof(ValueTuple<T1, T2, T3, T4, T5, T6>), true), (_lastParser, typeof(T7), true)];

    string ISequenceSourceable.CreateValue(string?[] values) => $"new {SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T4, T5, T7>))}({values[0]}.Item1, {values[0]}.Item2, {values[0]}.Item3, {values[0]}.Item4, {values[0]}.Item5, {values[1]})";

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail, this);

    public override string ToString() => FormatOnce($"{_parser} & {_lastParser} (skip)");

}

public sealed class SequenceSkipAnd<T1, T2, T3, T4, T5, T6, T7, T8> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T8>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable, ISequenceSourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> _parser;
    private readonly Parser<T8> _lastParser;

    // Whether the parsers are invoked without their value when it isn't used
    private readonly bool _parserWithoutValue;
    private readonly bool _lastParserWithoutValue;

    public SequenceSkipAnd(Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> parser, Parser<T8> lastParser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        _parserWithoutValue = _parser.CanParseWithoutValue;
        _lastParserWithoutValue = _lastParser.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser) && analysis.Invoke(_lastParser);

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T8>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T8>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5,
                    tupleResult.Value.Item6,
                    lastResult.Value
                );

                SequenceStart.Set(ref result, in tupleResult, in lastResult, tuple);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    internal override bool CanParseWithoutValue => _parserWithoutValue || _lastParserWithoutValue;

    // Parse, without the tuple
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T8>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parserWithoutValue ? _parser.ParseWithoutValue(context, ref tupleResult) : _parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T8>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                SequenceStart.SetRange(ref result, in tupleResult, in lastResult);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
        => SequenceSource.Generate(this, typeof(ValueTuple<T1, T2, T3, T4, T5, T6, T8>), "SequenceSkipAnd", context);

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser, _lastParser);

    IReadOnlyList<(object Parser, Type ValueType, bool Keep)> ISequenceSourceable.Parts => [(_parser, typeof(ValueTuple<T1, T2, T3, T4, T5, T6, T7>), true), (_lastParser, typeof(T8), true)];

    string ISequenceSourceable.CreateValue(string?[] values) => $"new {SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T4, T5, T6, T8>))}({values[0]}.Item1, {values[0]}.Item2, {values[0]}.Item3, {values[0]}.Item4, {values[0]}.Item5, {values[0]}.Item6, {values[1]})";

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail, this);

    public override string ToString() => FormatOnce($"{_parser} & {_lastParser} (skip)");

}
