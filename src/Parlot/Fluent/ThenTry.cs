using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Runtime.CompilerServices;

namespace Parlot.Fluent;

/// <summary>
/// Converts the value of a parser, or rejects it.
/// </summary>
/// <typeparam name="T">The type of the value to convert.</typeparam>
/// <typeparam name="U">The type of the converted value.</typeparam>
/// <param name="context">The current parse context.</param>
/// <param name="start">The offset where the value starts, after the white space the parser skipped.</param>
/// <param name="end">The offset where the value ends.</param>
/// <param name="value">The value to convert.</param>
/// <param name="result">The converted value, when the conversion succeeds.</param>
/// <returns><see langword="true"/> when the value is converted, <see langword="false"/> to fail the parser.</returns>
public delegate bool TryConvert<in T, U>(ParseContext context, int start, int end, T value, out U result);

/// <summary>
/// Converts the value of a parser like <see cref="Then{T, U}"/> with a conversion which can fail like a
/// <see cref="When{T}"/> condition: when it does, the cursor is moved back to where it was and the parser fails.
/// </summary>
/// <typeparam name="T">The input parser type.</typeparam>
/// <typeparam name="U">The output parser type.</typeparam>
public sealed class ThenTry<T, U> : Parser<U>, ISeekablePrefixes, ISourceable, ILoopAnalyzable
{
    private readonly TryConvert<T, U>? _tryConvert;
    private readonly Func<T, (bool Success, U Value)>? _convert;
    private readonly Parser<T> _parser;

    /// <summary>
    /// Creates a parser converting the value of <paramref name="parser"/> with <paramref name="conversion"/>, failing when
    /// it returns <see langword="false"/>.
    /// </summary>
    public ThenTry(Parser<T> parser, TryConvert<T, U> conversion)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _tryConvert = conversion ?? throw new ArgumentNullException(nameof(conversion));
    }

    /// <summary>
    /// Creates a parser converting the value of <paramref name="parser"/> with <paramref name="conversion"/>, failing when
    /// it returns a tuple whose <c>Success</c> is <see langword="false"/>.
    /// </summary>
    public ThenTry(Parser<T> parser, Func<T, (bool Success, U Value)> conversion)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _convert = conversion ?? throw new ArgumentNullException(nameof(conversion));
    }

    // The hints of the parser that reads first, read when they are asked for since it can be a Deferred set later
    private ForwardedHints? _hints;

    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser);

    // The value is always read since the conversion is invoked even when its result isn't used: no ParseWithoutValue

    public override bool Parse(ParseContext context, ref ParseResult<U> result)
    {
        if (context.IsCompacting)
        {
            return ParseCompacting(context, ref result);
        }

        context.EnterParser(this);

        var success = ParseValue(context, ref result);

        context.ExitParser(this);
        return success;
    }

    // A compacting buffer can't be read again from where the parser is invoked to find where a rejected value starts:
    // the white space the parser skips is skipped first, and the value is rejected where it is then read from. A
    // separate method, so that parsing a string doesn't keep the position across its calls.
    private bool ParseCompacting(ParseContext context, ref ParseResult<U> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;

        if (context.MaySkipWhiteSpace(cursor.Current) && _parser is ISeekable { SkipWhitespace: true })
        {
            context.SkipWhiteSpace();
        }

        var success = ParseValue(context, ref result);

        if (!success)
        {
            cursor.ResetPosition(start);
        }

        context.ExitParser(this);
        return success;
    }

    // Reads the value at the cursor and converts it. Inlined, so that Parse is compiled as when it held this code.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ParseValue(ParseContext context, ref ParseResult<U> result)
    {
        var start = context.Scanner.Cursor.Offset;
        var parsed = new ParseResult<T>();

        if (_parser.Parse(context, ref parsed))
        {
            // The conversion writes the value in place, the result being undefined when the parser fails
            if (_tryConvert != null ? _tryConvert(context, parsed.Start, parsed.End, parsed.Value, out result.Value) : Convert(parsed.Value, ref result))
            {
                result.Start = parsed.Start;
                result.End = parsed.End;

                return true;
            }

            Reject(context, start);
        }

        return false;
    }

    // The parser fails where it was invoked. A value that the conversion rejects is the failure, at its start, wherever
    // it was read up to, as in generated code and as one that a When rejects. Not inlined, so that only this path pays
    // for it. Written here rather than with ParseContext.RecordRejected(), which looks for white space without
    // reading the char at the cursor first: the call takes 2 to 3 ns more for a value without white space before it,
    // and about 1 ns more after white space (.NET 10).
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void Reject(ParseContext context, int start)
    {
        var cursor = context.Scanner.Cursor;
        var recorded = cursor.RecordedFailure;

        cursor.ResetPosition(start);
        cursor.RestoreRecordedFailure(recorded);

        var valueStart = start;

        // The value starts after the white space the parser skips. In compacting mode it was skipped before the value
        // was read. The char at the cursor is looked at first: a value with no white space before it, which nothing
        // was skipped before either, doesn't pay for asking the parser whether it skips some.
        if (!context.IsCompacting && context.MaySkipWhiteSpace(cursor.Current) && _parser is ISeekable { SkipWhitespace: true })
        {
            // The last white space that was skipped, as before a single token, isn't read again
            valueStart = context.SkippedWhiteSpaceEnd(start);

            if (valueStart < 0)
            {
                context.SkipWhiteSpace();
                valueStart = cursor.Offset;
                cursor.ResetPosition(start);
            }
        }

        cursor.RecordFailure(valueStart);
    }

    private bool Convert(T value, ref ParseResult<U> result)
    {
        var (success, converted) = _convert!(value);
        result.Value = converted;
        return success;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("ThenTry requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(U));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;
        var parsedTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var helperKey = $"{context.MethodNamePrefix}_ThenTry_{context.NextNumber()}";

        // The conversion reads the value whether this parser's value is used or not.
        // As in Then, the start of the value is read after the white space the parser skips: a SkipWhiteSpace wrapper is
        // unfolded and the white space skipped here. It is also where a rejected value fails, as in When, and the cursor
        // isn't moved back to read the value again, which a compacting buffer couldn't do.
        var inner = _parser is SkipWhiteSpace<T> { Parser: ISourceable contentSourceable } ? contentSourceable : sourceable;
        var helperName = context.WithDiscardResult(false, () => context.Helpers
            .GetOrCreate(inner, helperKey, parsedTypeName, () => inner.GenerateSource(context))
            .MethodName);

        var resetName = $"reset{context.NextNumber()}";
        var startName = $"start{context.NextNumber()}";
        var parsedName = $"parsed{context.NextNumber()}";
        var convertedName = $"converted{context.NextNumber()}";
        var recordedName = $"recorded{context.NextNumber()}";

        result.Body.Add($"var {resetName} = {cursorName}.Offset;");

        if (_parser is ISeekable { SkipWhitespace: true })
        {
            result.Body.Add($"if ({ctx}.MaySkipWhiteSpace({cursorName}.Current))");
            result.Body.Add("{");
            result.Body.Add($"    {ctx}.SkipWhiteSpace();");
            result.Body.Add("}");
        }

        result.Body.Add($"var {startName} = {cursorName}.Offset;");

        var convertedTarget = context.DiscardResult ? "_" : $"var {convertedName}";
        string condition;

        if (_tryConvert != null)
        {
            var lambdaName = context.RegisterLambda(_tryConvert);
            condition = $"{lambdaName}({ctx}, {startName}, {cursorName}.Offset, {parsedName}, out {convertedTarget})";
        }
        else
        {
            var lambdaName = context.RegisterLambda(_convert!);
            condition = $"{lambdaName}({parsedName}) is (true, {convertedTarget})";
        }

        // if (Helper(context, out var parsed))
        // {
        //     if (Convert(context, start, cursor.Offset, parsed, out var converted))
        //     {
        //         success = true;
        //         value = converted;
        //     }
        //     else
        //     {
        //         // A value that doesn't convert is the failure, at its start, wherever it was read up to
        //         var recorded = cursor.RecordedFailure;
        //         cursor.ResetPosition(reset);
        //         cursor.RestoreRecordedFailure(recorded);
        //         cursor.RecordFailure(start);
        //     }
        // }
        // else
        // {
        //     cursor.ResetPosition(reset);
        // }
        result.Body.Add($"if ({helperName}({ctx}, out var {parsedName}))");
        result.Body.Add("{");
        result.Body.Add($"    if ({condition})");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"        {result.ValueVariable} = {convertedName};");
        }
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        var {recordedName} = {cursorName}.RecordedFailure;");
        result.Body.Add($"        {cursorName}.ResetPosition({resetName});");
        result.Body.Add($"        {cursorName}.RestoreRecordedFailure({recordedName});");
        result.Body.Add($"        {cursorName}.RecordFailure({startName});");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({resetName});");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (ThenTry)";
}
