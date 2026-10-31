using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Scans a token at the start of <paramref name="span"/>.
/// </summary>
/// <param name="span">The text from the current position to the end of the available text.</param>
/// <returns>
/// The number of chars the token occupies, at most <c>span.Length</c>. <c>0</c> when there is no token.
/// A negative value when there is no token because <paramref name="span"/> ended, such that more text could make one.
/// </returns>
/// <remarks>
/// When parsing a stream, <paramref name="span"/> can end before the input does. A token that reaches the end of
/// <paramref name="span"/> is scanned again with more text. A scanner that decides based on the chars following its
/// token must thus either include them when they are missing, or return a negative value.
/// </remarks>
public delegate int SpanScanner(ReadOnlySpan<char> span);

/// <summary>
/// A parser that matches the token found by a <see cref="SpanScanner"/>.
/// </summary>
public sealed class SpanScannerLiteral : Parser<TextSpan>, ISeekable, ISourceable
{
    private readonly SpanScanner _scanner;

    /// <summary>
    /// Creates a parser that matches the token found by <paramref name="scanner"/>.
    /// </summary>
    /// <param name="scanner">The function that returns the length of the token at the start of a span.</param>
    /// <param name="startChars">The chars a token can start with, used to skip this parser when the current char is not one of them. Empty when unknown.</param>
    public SpanScannerLiteral(SpanScanner scanner, ReadOnlySpan<char> startChars = default)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));

        if (!startChars.IsEmpty)
        {
            CanSeek = true;
            ExpectedChars = startChars.ToArray();
        }

        Name = "SpanScanner";
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var span = cursor.Span;
        var size = _scanner(span);

        if (size <= 0)
        {
            if (size < 0)
            {
                // More text could have made a token
                cursor.MarkHitEnd();
            }

            context.ExitParser(this);
            return false;
        }

        if (size > span.Length)
        {
            ThrowInvalidSize(size, span.Length);
        }

        var start = cursor.Offset;

        // Reaching the end of the span marks the cursor such that the token is scanned again with more text.
        // The token can contain new lines, the cursor counts them when its position is read.
        cursor.AdvanceNoNewLines(size);

        result.Set(start, start + size, cursor.CreateSpan(start, size));

        context.ExitParser(this);
        return true;
    }

    private static void ThrowInvalidSize(int size, int length)
    {
        throw new InvalidOperationException($"The scanner returned a token of {size} chars while only {length} were available.");
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.IsCompacting)
        {
            return context.GenerateToken(this, typeof(TextSpan), () => GenerateSource(context));
        }

        var result = context.CreateResult(typeof(TextSpan));
        var cursorName = context.CursorName;
        var scannerName = context.RegisterLambda(_scanner);

        var spanName = $"span{context.NextNumber()}";
        var sizeName = $"size{context.NextNumber()}";
        var startName = $"start{context.NextNumber()}";

        result.Body.Add($"var {spanName} = {cursorName}.Span;");
        result.Body.Add($"var {sizeName} = {scannerName}({spanName});");
        result.Body.Add($"if ({sizeName} > 0)");
        result.Body.Add("{");
        result.Body.Add($"    if ({sizeName} > {spanName}.Length)");
        result.Body.Add("    {");
        result.Body.Add($"        throw new global::System.InvalidOperationException($\"The scanner returned a token of {{{sizeName}}} chars while only {{{spanName}.Length}} were available.\");");
        result.Body.Add("    }");
        result.Body.Add($"    var {startName} = {cursorName}.Offset;");
        result.Body.Add($"    {cursorName}.AdvanceNoNewLines({sizeName});");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    {result.ValueVariable} = {cursorName}.CreateSpan({startName}, {sizeName});");
        }
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");
        result.Body.Add($"else if ({sizeName} < 0)");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.MarkHitEnd();");
        result.Body.Add("}");

        return result;
    }
}
