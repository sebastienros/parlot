using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Globalization;
using Parlot.SourceGeneration;

namespace Parlot.Fluent;

public sealed class TextLiteral : Parser<string>, ISeekablePrefixes, ISourceable, ISpanSourceable
{
    private readonly StringComparison _comparisonType;
    private readonly bool _returnMatchedText;
    private readonly bool _returnText;
    private readonly string[]? _expectedPrefixes;

    // The invariant upper case of each char of the BMP, at the index of the char. It is only built for a
    // case-insensitive text which doesn't start with an ASCII char.
    private static string? _invariantUpperCase;

    public TextLiteral(string text, StringComparison comparisonType, bool returnMatchedText = false)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        _comparisonType = comparisonType;
        _returnMatchedText = returnMatchedText;

        // An ordinal match is the text itself, and case-insensitive matches return the canonical text by default.
        _returnText = comparisonType == StringComparison.Ordinal || (!returnMatchedText && comparisonType is StringComparison.OrdinalIgnoreCase
            or StringComparison.CurrentCultureIgnoreCase
            or StringComparison.InvariantCultureIgnoreCase);

        // A culture comparison ignores some chars, like the soft hyphen, and the current culture is the one of the
        // thread that parses, not the one of this thread. The chars such a text can start with are not known, only
        // an ordinal comparison starts with the first char of the text, or with its other cases.
        if (CanSeek = Text.Length > 0 && comparisonType is StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase)
        {
            ExpectedChars = comparisonType == StringComparison.OrdinalIgnoreCase
                ? OrdinalIgnoreCaseChars(Text)
                : [Text[0]];

            // An ordinal comparison only matches the text itself
            if (comparisonType == StringComparison.Ordinal)
            {
                _expectedPrefixes = [Text];
            }
        }
    }

    // The chars an ordinal case-insensitive comparison takes for the first one of a text. These are not the upper
    // and lower cases a culture gives: under a Turkish culture the upper case of 'i' is the dotted 'I' (U+0130),
    // which the comparison doesn't take for 'i', while it takes 'I'.
    private static char[] OrdinalIgnoreCaseChars(string text)
    {
        var first = text[0];

        if (first < 128)
        {
            // An ASCII letter only has its other case, on every runtime
            if (Character.IsInRange(first, 'a', 'z'))
            {
                return [(char)(first - 32), first];
            }

            if (Character.IsInRange(first, 'A', 'Z'))
            {
                return [first, (char)(first + 32)];
            }

            return [first];
        }

        var chars = new List<char>();

        if (char.IsSurrogate(first))
        {
            // A char beyond the BMP is compared as a whole. What is expected is the first half of each of its cases.
            var cased = char.IsSurrogatePair(text, 0) ? text.Substring(0, 2) : text.Substring(0, 1);

            chars.Add(first);

            foreach (var other in new[] { cased.ToUpperInvariant()[0], cased.ToLowerInvariant()[0] })
            {
                if (!chars.Contains(other))
                {
                    chars.Add(other);
                }
            }

            return [.. chars];
        }

        // Other chars can have more than one other case: the Greek sigma (U+03C3), final sigma (U+03C2) and capital
        // sigma (U+03A3) are the same to the comparison, and so are the micro sign (U+00B5) and the Greek mu (U+03BC).
        // The candidates are the chars with the same upper case, and the comparison decides.

        var upperCase = _invariantUpperCase;

        if (upperCase == null)
        {
            var all = new char[char.MaxValue + 1];

            for (var i = 0; i < all.Length; i++)
            {
                // Two surrogates in a row would be read as a single char
                all[i] = char.IsSurrogate((char)i) ? '\0' : (char)i;
            }

            // A single call for all the chars, since each call goes through the globalization library
            _invariantUpperCase = upperCase = new string(all).ToUpperInvariant();
        }

        var expected = first.ToString();
        var firstUpperCase = upperCase[first];

        for (var i = 0; i < upperCase.Length; i++)
        {
            if (upperCase[i] == firstUpperCase && string.Equals(((char)i).ToString(), expected, StringComparison.OrdinalIgnoreCase))
            {
                chars.Add((char)i);
            }
        }

        return [.. chars];
    }

    public string Text { get; }

    internal StringComparison Comparison => _comparisonType;

    // Whether the canonical text is returned rather than the matched one
    internal bool ReturnsText => _returnText;

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    string[]? ISeekablePrefixes.ExpectedPrefixes => _expectedPrefixes;

    public override bool Parse(ParseContext context, ref ParseResult<string> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;

        var span = Text.AsSpan();

        if (cursor.Match(span, _comparisonType))
        {
            var start = cursor.Offset;

            // The text was matched so it is in the buffer. It can contain new lines, the cursor counts them when its position is read.
            cursor.AdvanceNoNewLines(Text.Length);

            var end = cursor.Offset;

            if (_returnText)
            {
                // Return the canonical source text (avoids a second comparison and an allocation).
                result.Set(start, end, Text);
            }
            else
            {
                var buffer = cursor.Buffer;
                var parsedText = cursor.GetSpan(start, end - start);

                result.Set(start, end, parsedText.Equals(Text, StringComparison.Ordinal)
                    ? Text
                    : start == cursor.BufferStart && end - start == buffer.Length
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

        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(string));

        var textLiteral = LiteralHelper.StringToLiteral(Text);
        var lengthLiteral = Text.Length.ToString(CultureInfo.InvariantCulture);
        var comparison = $"global::System.StringComparison.{_comparisonType}";
        var isNotOrdinal = _comparisonType != StringComparison.Ordinal;
        var ignoreCase = _comparisonType is StringComparison.OrdinalIgnoreCase
            or StringComparison.CurrentCultureIgnoreCase
            or StringComparison.InvariantCultureIgnoreCase;

        var startName = $"start{context.NextNumber()}";

        // Use direct SourceResult construction for early return optimization
        var result = new SourceResult(
            successVariable: "success",  // Not used with early returns
            valueVariable: "value",
            valueTypeName: valueTypeName);

        result.Body.Add($"if ({cursorName}.Match({textLiteral}.AsSpan(), {comparison}))");
        result.Body.Add("{");
        var shouldReturnMatchedText = !context.DiscardResult && isNotOrdinal && (!ignoreCase || _returnMatchedText);
        if (shouldReturnMatchedText)
        {
            result.Body.Add($"    var {startName} = {cursorName}.Offset;");
        }
        result.Body.Add($"    {cursorName}.AdvanceNoNewLines({lengthLiteral});");

        // Default behavior for case-insensitive comparisons is to return the canonical source text (no allocation).
        if (context.DiscardResult)
        {
            result.Body.Add($"    {result.ValueVariable} = default;");
        }
        else if (shouldReturnMatchedText)
        {
            var bufferName = $"buffer{context.NextNumber()}";
            var parsedTextName = $"parsedText{context.NextNumber()}";
            result.Body.Add($"    var {bufferName} = {scannerName}.Buffer;");
            result.Body.Add($"    var {parsedTextName} = {cursorName}.GetSpan({startName}, {lengthLiteral});");
            result.Body.Add($"    {result.ValueVariable} = {parsedTextName}.SequenceEqual({textLiteral}.AsSpan())");
            result.Body.Add($"        ? {textLiteral}");
            result.Body.Add($"        : {lengthLiteral} == {bufferName}.Length ? {bufferName} : {parsedTextName}.ToString();");
        }
        else
        {
            result.Body.Add($"    {result.ValueVariable} = {textLiteral};");
        }
        result.Body.Add("    return true;");
        result.Body.Add("}");
        result.Body.Add($"{result.ValueVariable} = default;");
        result.Body.Add("return false;");

        return result;
    }

    // Only ordinal texts, whose value is the text itself
    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => _comparisonType == StringComparison.Ordinal ? 1 : -1;

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
    {
        var span = writer.Span;
        var pos = writer.Pos;
        var textLiteral = LiteralHelper.StringToLiteral(Text);

        if (Text.Length == 1)
        {
            // The unsigned compare of the position lets the JIT remove the bounds check of the span
            writer.BeginBlock($"if ((uint){pos} >= (uint){span}.Length || {span}[{pos}] != {LiteralHelper.CharToLiteral(Text[0])})");
            writer.Fail(fail);
            writer.EndBlock();
            writer.Add($"{pos}++;");
        }
        else if (Text.Length > 1)
        {
            var lengthLiteral = Text.Length.ToString(CultureInfo.InvariantCulture);

            // The JIT compares the span with the constant text without a loop
            writer.BeginBlock($"if (!global::System.MemoryExtensions.StartsWith({span}.Slice({pos}), {textLiteral}.AsSpan()))");
            // A shorter remainder only proves a mismatch when it already differs from the text, like Cursor.Match
            writer.Add($"if ({span}.Length - {pos} < {lengthLiteral} && global::System.MemoryExtensions.StartsWith({textLiteral}.AsSpan(), {span}.Slice({pos}))) {writer.MarkHitEnd}");
            writer.Add(fail.Goto());
            writer.EndBlock();
            writer.Add($"{pos} += {lengthLiteral};");
        }

        if (valueVariable != null)
        {
            writer.Add($"{valueVariable} = {textLiteral};");
        }
    }

public override string ToString() => $"Text(\"{Text}\")";
}
