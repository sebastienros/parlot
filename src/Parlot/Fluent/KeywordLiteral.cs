using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Parses a keyword (text followed by non-letter character or EOF).
/// This is a specialized parser that can be source-generated without requiring lambda tracking.
/// </summary>
public sealed class KeywordLiteral : Parser<string>, ISeekable, ISourceable
{
    // The text parser is kept for the source generator, which emits it as a helper; the runtime matches the text inline
    private readonly TextLiteral _textLiteral;
    private readonly bool _hasNewLines;
    private readonly bool _returnText;

    public KeywordLiteral(string text, StringComparison comparison = StringComparison.Ordinal, bool returnMatchedText = false)
    {
        _textLiteral = new TextLiteral(text, comparison, returnMatchedText: returnMatchedText);
        _hasNewLines = _textLiteral.HasNewLines;
        _returnText = _textLiteral.ReturnsText;
        Text = text;
        Comparison = comparison;
        ReturnMatchedText = returnMatchedText;
        CanSeek = _textLiteral.CanSeek;
        ExpectedChars = _textLiteral.ExpectedChars;
        SkipWhitespace = _textLiteral.SkipWhitespace;
    }

    public string Text { get; }

    public StringComparison Comparison { get; }

    public bool ReturnMatchedText { get; }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; }

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<string> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var text = Text;

        // The text and then the keyword boundary are checked before the cursor moves, such that a mismatch
        // needs no reset and no string is built for a text that is not a keyword. Match marks HitEnd when
        // the buffer ends inside the text.
        if (cursor.Match(text.AsSpan(), Comparison))
        {
            var length = text.Length;
            var span = cursor.Span;

            // The next char must not be a letter (keyword boundary). When the text ends the buffer the cursor
            // reaches the end, which marks HitEnd.
            if (span.Length > length)
            {
                var next = span[length];

                if (Character.IsInRange(next, 'a', 'z') || Character.IsInRange(next, 'A', 'Z'))
                {
                    context.ExitParser(this);
                    return false;
                }
            }

            var start = cursor.Offset;

            if (_hasNewLines)
            {
                cursor.Advance(length);
            }
            else
            {
                cursor.AdvanceNoNewLines(length);
            }

            if (_returnText)
            {
                // Return the canonical source text (avoids a second comparison and an allocation).
                result.Set(start, start + length, text);
            }
            else
            {
                var buffer = cursor.Buffer;
                var parsedText = cursor.GetSpan(start, length);

                result.Set(start, start + length, parsedText.Equals(text, StringComparison.Ordinal)
                    ? text
                    : start == cursor.BufferStart && length == buffer.Length
                        ? buffer
                        : parsedText.ToString());
            }

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.IsCompacting)
        {
            return context.GenerateToken(this, typeof(string), () => GenerateSource(context));
        }

        if (_textLiteral is not ISourceable sourceable)
        {
            throw new NotSupportedException("KeywordLiteral requires a source-generatable text parser.");
        }

        var result = context.CreateResult(typeof(string));
        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(string));

        var startName = $"start{context.NextNumber()}";
        result.Body.Add($"var {startName} = {cursorName}.Position;");

        // Use helper for the text literal
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Keyword", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        var outTarget = context.DiscardResult ? "_" : result.ValueVariable;
        
        // if (Helper(context, out var innerValue))
        // {
        //     // Check keyword boundary: cursor.Eof || (!IsInRange(current, 'a', 'z') && !IsInRange(current, 'A', 'Z'))
        //     if (cursor.Eof || (!Character.IsInRange(cursor.Current, 'a', 'z') && !Character.IsInRange(cursor.Current, 'A', 'Z')))
        //     {
        //         success = true;
        //         value = innerValue;
        //     }
        //     else
        //     {
        //         cursor.ResetPosition(start);
        //     }
        // }
        
        result.Body.Add($"if ({helperName}({context.ParseContextName}, out {outTarget}))");
        result.Body.Add("{");
        result.Body.Add($"    if ({cursorName}.Eof || (!Parlot.Character.IsInRange({cursorName}.Current, 'a', 'z') && !Parlot.Character.IsInRange({cursorName}.Current, 'A', 'Z')))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"Keyword(\"{Text}\")";
}
