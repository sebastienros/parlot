using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Matches the end of the input without consuming text or skipping whitespace.
/// </summary>
public sealed class Eof : Parser<object?>, ISourceable, ILoopAnalyzable
{
    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => true;

    /// <inheritdoc />
    public override bool Parse(ParseContext context, ref ParseResult<object?> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;

        if (cursor.Eof)
        {
            cursor.MarkHitEnd();
            result.Set(cursor.Offset, cursor.Offset, null);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    /// <inheritdoc />
    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.IsCompacting)
        {
            return context.GenerateToken(this, typeof(object), () => GenerateSource(context));
        }

        var result = context.CreateResult(typeof(object));
        result.Body.Add($"if ({context.CursorName}.Eof)");
        result.Body.Add("{");
        result.Body.Add($"    {context.CursorName}.MarkHitEnd();");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add($"    {result.ValueVariable} = null;");
        result.Body.Add("}");

        return result;
    }

    /// <inheritdoc />
    public override string ToString() => "Eof";
}

/// <summary>
/// Successful when the cursor is at the end of the string.
/// </summary>
public sealed class Eof<T> : Parser<T>, ISeekable, ISourceable, ILoopAnalyzable
{
    private readonly Parser<T> _parser;

    public Eof(Parser<T> parser)
    {
        _parser = parser;
    }

    // The parser reads first, the end of the input is only checked after it. Its hints are read when they are asked for
    // since it can be a Deferred set later.
    private ForwardedHints? _hints;

    /// <inheritdoc />
    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    /// <inheritdoc />
    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    /// <inheritdoc />
    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser);

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;

        if (_parser.Parse(context, ref result))
        {
            if (cursor.Eof)
            {
                context.ExitParser(this);
                return true;
            }

            // The parser matched but more text follows
            cursor.ResetPosition(start);
        }

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Eof requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T));
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Eof", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // if (Helper(context, out var innerValue) && cursor.Eof)
        // {
        //     success = true;
        //     value = innerValue;
        // }
        var startName = $"start{context.NextNumber()}";
        result.Body.Add($"var {startName} = {context.CursorName}.Offset;");

        if (context.DiscardResult)
        {
            result.Body.Add($"if ({helperName}({context.ParseContextName}, out _) && {context.CursorName}.Eof)");
        }
        else
        {
            result.Body.Add($"if ({helperName}({context.ParseContextName}, out {result.ValueVariable}) && {context.CursorName}.Eof)");
        }
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {context.CursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (Eof)";
}
