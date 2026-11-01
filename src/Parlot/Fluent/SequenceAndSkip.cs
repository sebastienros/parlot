using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public sealed class SequenceAndSkip<T1, T2> : Parser<T1>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly Parser<T1> _parser1;
    private readonly Parser<T2> _parser2;

    // Whether the parsers are invoked without their value when it isn't used
    private readonly bool _withoutValue1;
    private readonly bool _withoutValue2;

    public SequenceAndSkip(Parser<T1> parser1, Parser<T2> parser2)
    {
        _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
        _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
        _withoutValue1 = _parser1.CanParseWithoutValue;
        _withoutValue2 = _parser2.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser1).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser1).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser1).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser1);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser1) && analysis.Invoke(_parser2);

    public override bool Parse(ParseContext context, ref ParseResult<T1> result)
    {
        context.EnterParser(this);

        var parseResult1 = new ParseResult<T1>();

        var start = context.Scanner.Cursor.Offset;

        if (_parser1.Parse(context, ref parseResult1))
        {
            var parseResult2 = new ParseResult<T2>();

            if (_withoutValue2 ? _parser2.ParseWithoutValue(context, ref parseResult2) : _parser2.Parse(context, ref parseResult2))
            {
                result.Set(parseResult1.Start, parseResult2.End, parseResult1.Value);

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
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<T1> result)
    {
        context.EnterParser(this);

        var parseResult1 = new ParseResult<T1>();

        var start = context.Scanner.Cursor.Offset;

        if (_withoutValue1 ? _parser1.ParseWithoutValue(context, ref parseResult1) : _parser1.Parse(context, ref parseResult1))
        {
            var parseResult2 = new ParseResult<T2>();

            if (_withoutValue2 ? _parser2.ParseWithoutValue(context, ref parseResult2) : _parser2.Parse(context, ref parseResult2))
            {
                result.Start = parseResult1.Start;
                result.End = parseResult2.End;

                context.ExitParser(this);
                return true;
            }

            context.Scanner.Cursor.ResetPosition(start);
        }

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser1 is not ISourceable parser1 || _parser2 is not ISourceable parser2)
        {
            throw new NotSupportedException("SequenceAndSkip requires source-generatable parsers.");
        }

        if (SpanSource.TryGenerate(this, typeof(T1), context) is { } spanResult)
        {
            return spanResult;
        }

        var result = context.CreateResult(typeof(T1));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";

        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add($"{result.SuccessVariable} = false;");

        string Helper(ISourceable p, Type valueType, string suffix)
        {
            var valueTypeName = SourceGenerationContext.GetTypeName(valueType);
            return context.Helpers
                .GetOrCreate(p, $"{context.MethodNamePrefix}_SequenceAndSkip_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helper1 = Helper(parser1, typeof(T1), "P1");
        var helper2 = context.WithDiscardResult(true, () => Helper(parser2, typeof(T2), "P2"));

        var outTarget = context.DiscardResult ? "_" : result.ValueVariable;
        result.Body.Add($"if ({helper1}({context.ParseContextName}, out {outTarget}))");
        result.Body.Add("{");
        result.Body.Add($"    if ({helper2}({context.ParseContextName}, out _))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser1, _parser2);

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail,
            [(_parser1, typeof(T1), true), (_parser2, typeof(T2), false)],
            static values => values[0]!);

    public override string ToString() => $"{_parser1} & (skip) {_parser2}";
}

public sealed class SequenceAndSkip<T1, T2, T3> : Parser<ValueTuple<T1, T2>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly Parser<ValueTuple<T1, T2>> _parser;
    private readonly Parser<T3> _lastParser;

    // Whether the parsers are invoked without their value when it isn't used
    private readonly bool _parserWithoutValue;
    private readonly bool _lastParserWithoutValue;

    public SequenceAndSkip(Parser<ValueTuple<T1, T2>>
        parser,
        Parser<T3> lastParser
        )
    {
        _parser = parser;
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        _parserWithoutValue = parser is not null && parser.CanParseWithoutValue;
        _lastParserWithoutValue = _lastParser.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser) && analysis.Invoke(_lastParser);

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T3>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2
                );

                result.Set(tupleResult.Start, lastResult.End, tuple);

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
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<ValueTuple<T1, T2>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parserWithoutValue ? _parser.ParseWithoutValue(context, ref tupleResult) : _parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T3>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                result.Start = tupleResult.Start;
                result.End = lastResult.End;

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parser || _lastParser is not ISourceable lastParser)
        {
            throw new NotSupportedException("SequenceAndSkip requires source-generatable parsers.");
        }

        if (SpanSource.TryGenerate(this, typeof(ValueTuple<T1, T2>), context) is { } spanResult)
        {
            return spanResult;
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2>));

        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add($"{result.SuccessVariable} = false;");

        string Helper(ISourceable p, Type valueType, string suffix)
        {
            var valueTypeName = SourceGenerationContext.GetTypeName(valueType);
            return context.Helpers
                .GetOrCreate(p, $"{context.MethodNamePrefix}_SequenceAndSkip_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, typeof(ValueTuple<T1, T2>), "Parser");
        var helperLast = context.WithDiscardResult(true, () => Helper(lastParser, typeof(T3), "Last"));

        result.Body.Add($"if ({helperParser}({context.ParseContextName}, out var hpValue))");
        result.Body.Add("{");
        result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out _))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hpValue.Item1, hpValue.Item2);");
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser, _lastParser);

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail,
            [(_parser, typeof(ValueTuple<T1, T2>), true), (_lastParser, typeof(T3), false)],
            static values => values[0]!);

    public override string ToString() => $"{_parser} & (skip) {_lastParser}";

}

public sealed class SequenceAndSkip<T1, T2, T3, T4> : Parser<ValueTuple<T1, T2, T3>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3>> _parser;
    private readonly Parser<T4> _lastParser;

    // Whether the parsers are invoked without their value when it isn't used
    private readonly bool _parserWithoutValue;
    private readonly bool _lastParserWithoutValue;

    public SequenceAndSkip(Parser<ValueTuple<T1, T2, T3>> parser, Parser<T4> lastParser)
    {
        _parser = parser;
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        _parserWithoutValue = parser is not null && parser.CanParseWithoutValue;
        _lastParserWithoutValue = _lastParser.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser) && analysis.Invoke(_lastParser);

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T4>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3
                );

                result.Set(tupleResult.Start, lastResult.End, tuple);

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
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parserWithoutValue ? _parser.ParseWithoutValue(context, ref tupleResult) : _parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T4>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                result.Start = tupleResult.Start;
                result.End = lastResult.End;

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parser || _lastParser is not ISourceable lastParser)
        {
            throw new NotSupportedException("SequenceAndSkip requires source-generatable parsers.");
        }

        if (SpanSource.TryGenerate(this, typeof(ValueTuple<T1, T2, T3>), context) is { } spanResult)
        {
            return spanResult;
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T3>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3>));

        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add($"{result.SuccessVariable} = false;");

        string Helper(ISourceable p, Type valueType, string suffix)
        {
            var valueTypeName = SourceGenerationContext.GetTypeName(valueType);
            return context.Helpers
                .GetOrCreate(p, $"{context.MethodNamePrefix}_SequenceAndSkip_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, typeof(ValueTuple<T1, T2, T3>), "Parser");
        var helperLast = context.WithDiscardResult(true, () => Helper(lastParser, typeof(T4), "Last"));

        result.Body.Add($"if ({helperParser}({context.ParseContextName}, out var hpValue))");
        result.Body.Add("{");
        result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out _))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hpValue.Item1, hpValue.Item2, hpValue.Item3);");
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser, _lastParser);

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail,
            [(_parser, typeof(ValueTuple<T1, T2, T3>), true), (_lastParser, typeof(T4), false)],
            static values => values[0]!);

    public override string ToString() => $"{_parser} & (skip) {_lastParser}";
}

public sealed class SequenceAndSkip<T1, T2, T3, T4, T5> : Parser<ValueTuple<T1, T2, T3, T4>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4>> _parser;
    private readonly Parser<T5> _lastParser;

    // Whether the parsers are invoked without their value when it isn't used
    private readonly bool _parserWithoutValue;
    private readonly bool _lastParserWithoutValue;

    public SequenceAndSkip(Parser<ValueTuple<T1, T2, T3, T4>> parser, Parser<T5> lastParser)
    {
        _parser = parser;
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        _parserWithoutValue = parser is not null && parser.CanParseWithoutValue;
        _lastParserWithoutValue = _lastParser.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser) && analysis.Invoke(_lastParser);

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T5>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4
                );

                result.Set(tupleResult.Start, lastResult.End, tuple);

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
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parserWithoutValue ? _parser.ParseWithoutValue(context, ref tupleResult) : _parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T5>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                result.Start = tupleResult.Start;
                result.End = lastResult.End;

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parser || _lastParser is not ISourceable lastParser)
        {
            throw new NotSupportedException("SequenceAndSkip requires source-generatable parsers.");
        }

        if (SpanSource.TryGenerate(this, typeof(ValueTuple<T1, T2, T3, T4>), context) is { } spanResult)
        {
            return spanResult;
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T3, T4>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T4>));

        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add($"{result.SuccessVariable} = false;");

        string Helper(ISourceable p, Type valueType, string suffix)
        {
            var valueTypeName = SourceGenerationContext.GetTypeName(valueType);
            return context.Helpers
                .GetOrCreate(p, $"{context.MethodNamePrefix}_SequenceAndSkip_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, typeof(ValueTuple<T1, T2, T3, T4>), "Parser");
        var helperLast = context.WithDiscardResult(true, () => Helper(lastParser, typeof(T5), "Last"));

        result.Body.Add($"if ({helperParser}({context.ParseContextName}, out var hpValue))");
        result.Body.Add("{");
        result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out _))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hpValue.Item1, hpValue.Item2, hpValue.Item3, hpValue.Item4);");
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser, _lastParser);

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail,
            [(_parser, typeof(ValueTuple<T1, T2, T3, T4>), true), (_lastParser, typeof(T5), false)],
            static values => values[0]!);

    public override string ToString() => $"{_parser} & (skip) {_lastParser}";

}

public sealed class SequenceAndSkip<T1, T2, T3, T4, T5, T6> : Parser<ValueTuple<T1, T2, T3, T4, T5>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4, T5>> _parser;
    private readonly Parser<T6> _lastParser;

    // Whether the parsers are invoked without their value when it isn't used
    private readonly bool _parserWithoutValue;
    private readonly bool _lastParserWithoutValue;

    public SequenceAndSkip(Parser<ValueTuple<T1, T2, T3, T4, T5>> parser, Parser<T6> lastParser)
    {
        _parser = parser;
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        _parserWithoutValue = parser is not null && parser.CanParseWithoutValue;
        _lastParserWithoutValue = _lastParser.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser) && analysis.Invoke(_lastParser);

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T6>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5
                );

                result.Set(tupleResult.Start, lastResult.End, tuple);

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
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parserWithoutValue ? _parser.ParseWithoutValue(context, ref tupleResult) : _parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T6>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                result.Start = tupleResult.Start;
                result.End = lastResult.End;

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parser || _lastParser is not ISourceable lastParser)
        {
            throw new NotSupportedException("SequenceAndSkip requires source-generatable parsers.");
        }

        if (SpanSource.TryGenerate(this, typeof(ValueTuple<T1, T2, T3, T4, T5>), context) is { } spanResult)
        {
            return spanResult;
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T3, T4, T5>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T4, T5>));

        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add($"{result.SuccessVariable} = false;");

        string Helper(ISourceable p, Type valueType, string suffix)
        {
            var valueTypeName = SourceGenerationContext.GetTypeName(valueType);
            return context.Helpers
                .GetOrCreate(p, $"{context.MethodNamePrefix}_SequenceAndSkip_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, typeof(ValueTuple<T1, T2, T3, T4, T5>), "Parser");
        var helperLast = context.WithDiscardResult(true, () => Helper(lastParser, typeof(T6), "Last"));

        result.Body.Add($"if ({helperParser}({context.ParseContextName}, out var hpValue))");
        result.Body.Add("{");
        result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out _))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hpValue.Item1, hpValue.Item2, hpValue.Item3, hpValue.Item4, hpValue.Item5);");
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser, _lastParser);

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail,
            [(_parser, typeof(ValueTuple<T1, T2, T3, T4, T5>), true), (_lastParser, typeof(T6), false)],
            static values => values[0]!);

    public override string ToString() => $"{_parser} & (skip) {_lastParser}";
}

public sealed class SequenceAndSkip<T1, T2, T3, T4, T5, T6, T7> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> _parser;
    private readonly Parser<T7> _lastParser;

    // Whether the parsers are invoked without their value when it isn't used
    private readonly bool _parserWithoutValue;
    private readonly bool _lastParserWithoutValue;

    public SequenceAndSkip(Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> parser, Parser<T7> lastParser)
    {
        _parser = parser;
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        _parserWithoutValue = parser is not null && parser.CanParseWithoutValue;
        _lastParserWithoutValue = _lastParser.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser) && analysis.Invoke(_lastParser);

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T7>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5,
                    tupleResult.Value.Item6
                );

                result.Set(tupleResult.Start, lastResult.End, tuple);

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
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parserWithoutValue ? _parser.ParseWithoutValue(context, ref tupleResult) : _parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T7>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                result.Start = tupleResult.Start;
                result.End = lastResult.End;

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parser || _lastParser is not ISourceable lastParser)
        {
            throw new NotSupportedException("SequenceAndSkip requires source-generatable parsers.");
        }

        if (SpanSource.TryGenerate(this, typeof(ValueTuple<T1, T2, T3, T4, T5, T6>), context) is { } spanResult)
        {
            return spanResult;
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T3, T4, T5, T6>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T4, T5, T6>));

        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add($"{result.SuccessVariable} = false;");

        string Helper(ISourceable p, Type valueType, string suffix)
        {
            var valueTypeName = SourceGenerationContext.GetTypeName(valueType);
            return context.Helpers
                .GetOrCreate(p, $"{context.MethodNamePrefix}_SequenceAndSkip_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, typeof(ValueTuple<T1, T2, T3, T4, T5, T6>), "Parser");
        var helperLast = context.WithDiscardResult(true, () => Helper(lastParser, typeof(T7), "Last"));

        result.Body.Add($"if ({helperParser}({context.ParseContextName}, out var hpValue))");
        result.Body.Add("{");
        result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out _))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hpValue.Item1, hpValue.Item2, hpValue.Item3, hpValue.Item4, hpValue.Item5, hpValue.Item6);");
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser, _lastParser);

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail,
            [(_parser, typeof(ValueTuple<T1, T2, T3, T4, T5, T6>), true), (_lastParser, typeof(T7), false)],
            static values => values[0]!);

    public override string ToString() => $"{_parser} & (skip) {_lastParser}";

}

public sealed class SequenceAndSkip<T1, T2, T3, T4, T5, T6, T7, T8> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> _parser;
    private readonly Parser<T8> _lastParser;

    // Whether the parsers are invoked without their value when it isn't used
    private readonly bool _parserWithoutValue;
    private readonly bool _lastParserWithoutValue;

    public SequenceAndSkip(Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> parser, Parser<T8> lastParser)
    {
        _parser = parser;
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        _parserWithoutValue = parser is not null && parser.CanParseWithoutValue;
        _lastParserWithoutValue = _lastParser.CanParseWithoutValue;
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser) && analysis.Invoke(_lastParser);

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T8>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                var tuple = (
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5,
                    tupleResult.Value.Item6,
                    tupleResult.Value.Item7
                );

                result.Set(tupleResult.Start, lastResult.End, tuple);

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
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>();

        var start = context.Scanner.Cursor.Offset;

        if (_parserWithoutValue ? _parser.ParseWithoutValue(context, ref tupleResult) : _parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T8>();

            if (_lastParserWithoutValue ? _lastParser.ParseWithoutValue(context, ref lastResult) : _lastParser.Parse(context, ref lastResult))
            {
                result.Start = tupleResult.Start;
                result.End = lastResult.End;

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parser || _lastParser is not ISourceable lastParser)
        {
            throw new NotSupportedException("SequenceAndSkip requires source-generatable parsers.");
        }

        if (SpanSource.TryGenerate(this, typeof(ValueTuple<T1, T2, T3, T4, T5, T6, T7>), context) is { } spanResult)
        {
            return spanResult;
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T3, T4, T5, T6, T7>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T4, T5, T6, T7>));

        result.Body.Add($"var {startName} = {cursorName}.Offset;");
        result.Body.Add($"{result.SuccessVariable} = false;");

        string Helper(ISourceable p, Type valueType, string suffix)
        {
            var valueTypeName = SourceGenerationContext.GetTypeName(valueType);
            return context.Helpers
                .GetOrCreate(p, $"{context.MethodNamePrefix}_SequenceAndSkip_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, typeof(ValueTuple<T1, T2, T3, T4, T5, T6, T7>), "Parser");
        var helperLast = context.WithDiscardResult(true, () => Helper(lastParser, typeof(T8), "Last"));

        result.Body.Add($"if ({helperParser}({context.ParseContextName}, out var hpValue))");
        result.Body.Add("{");
        result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out _))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hpValue.Item1, hpValue.Item2, hpValue.Item3, hpValue.Item4, hpValue.Item5, hpValue.Item6, hpValue.Item7);");
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => SpanSource.GetWeight(context, _parser, _lastParser);

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => SpanSource.GenerateSequence(writer, valueVariable, fail,
            [(_parser, typeof(ValueTuple<T1, T2, T3, T4, T5, T6, T7>), true), (_lastParser, typeof(T8), false)],
            static values => values[0]!);

    public override string ToString() => $"{_parser} & (skip) {_lastParser}";

}
