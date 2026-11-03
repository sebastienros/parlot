using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Parlot.Fluent;

/// <summary>
/// Parses a keyword: a text that is not followed by a char that continues an identifier. By default these chars
/// are the ASCII letters; a custom set can be given, as a list of chars or as ranges, e.g. to also reject digits,
/// <c>_</c> or <c>-</c>, or the non-ASCII chars.
/// This is a specialized parser that can be source-generated without requiring lambda tracking.
/// </summary>
public sealed class KeywordLiteral : Parser<string>, ISeekablePrefixes, ISourceable, ILoopAnalyzable
{
    // The text parser is kept for the source generator, which emits it as a helper; the runtime matches the text inline
    private readonly TextLiteral _textLiteral;
    private readonly bool _returnText;

    // The chars that continue an identifier, which a keyword must not be followed by: a bitset of the ASCII chars,
    // 0-63 in _identifierPartLow and 64-127 in _identifierPartHigh, and the other chars in _identifierPartNonAscii,
    // as the first and last chars of sorted ranges (null when there are none). The default set is the ASCII letters.
    private readonly ulong _identifierPartLow;
    private readonly ulong _identifierPartHigh = AsciiLettersHigh;
    private readonly char[]? _identifierPartNonAscii;

    // 'A'-'Z' and 'a'-'z' are 65-90 and 97-122, i.e. bits 1-26 and 33-58 of the high half
    private const ulong AsciiLettersHigh = 0x07FFFFFE07FFFFFEUL;

    public KeywordLiteral(string text, StringComparison comparison = StringComparison.Ordinal, bool returnMatchedText = false)
    {
        _textLiteral = new TextLiteral(text, comparison, returnMatchedText: returnMatchedText);
        _returnText = _textLiteral.ReturnsText;
        Text = text;
        Comparison = comparison;
        ReturnMatchedText = returnMatchedText;
        CanSeek = _textLiteral.CanSeek;
        ExpectedChars = _textLiteral.ExpectedChars;
        SkipWhitespace = _textLiteral.SkipWhitespace;
    }

    /// <summary>
    /// Creates a keyword parser that fails when the text is followed by one of <paramref name="identifierPart"/>.
    /// </summary>
    /// <param name="text">The keyword.</param>
    /// <param name="identifierPart">The chars that continue an identifier. They replace the default ASCII letters.</param>
    /// <param name="comparison">How the text is compared.</param>
    /// <param name="returnMatchedText">Whether to return the input's text rather than <paramref name="text"/>.</param>
    public KeywordLiteral(string text, ReadOnlySpan<char> identifierPart, StringComparison comparison = StringComparison.Ordinal, bool returnMatchedText = false)
        : this(text, comparison, returnMatchedText)
    {
        var ranges = new (char First, char Last)[identifierPart.Length];
        for (var i = 0; i < identifierPart.Length; i++)
        {
            ranges[i] = (identifierPart[i], identifierPart[i]);
        }

        (_identifierPartLow, _identifierPartHigh, _identifierPartNonAscii) = BuildIdentifierPart(ranges);
        IdentifierPart = identifierPart.ToString();
    }

    /// <summary>
    /// Creates a keyword parser that fails when the text is followed by a char of <paramref name="identifierPartRanges"/>.
    /// </summary>
    /// <param name="text">The keyword.</param>
    /// <param name="identifierPartRanges">The ranges of chars that continue an identifier, both ends included. They replace the default ASCII letters.</param>
    /// <param name="comparison">How the text is compared.</param>
    /// <param name="returnMatchedText">Whether to return the input's text rather than <paramref name="text"/>.</param>
    public KeywordLiteral(string text, ReadOnlySpan<(char First, char Last)> identifierPartRanges, StringComparison comparison = StringComparison.Ordinal, bool returnMatchedText = false)
        : this(text, comparison, returnMatchedText)
    {
        foreach (var (first, last) in identifierPartRanges)
        {
            if (first > last)
            {
                throw new ArgumentException($"The range '\\u{(int)first:x4}'-'\\u{(int)last:x4}' is empty.", nameof(identifierPartRanges));
            }
        }

        (_identifierPartLow, _identifierPartHigh, _identifierPartNonAscii) = BuildIdentifierPart(identifierPartRanges.ToArray());
        IdentifierPartRanges = identifierPartRanges.ToArray();
    }

    private static (ulong Low, ulong High, char[]? NonAscii) BuildIdentifierPart((char First, char Last)[] ranges)
    {
        var low = 0UL;
        var high = 0UL;
        var nonAscii = new List<char>();

        foreach (var (first, last) in ranges.OrderBy(static range => range.First))
        {
            for (var c = (int)first; c <= last && c < 128; c++)
            {
                if (c < 64)
                {
                    low |= 1UL << c;
                }
                else
                {
                    high |= 1UL << (c - 64);
                }
            }

            if (last >= 128)
            {
                var start = first < 128 ? (char)128 : first;

                // Adjacent or overlapping with the previous range
                if (nonAscii.Count > 0 && start - 1 <= nonAscii[nonAscii.Count - 1])
                {
                    if (last > nonAscii[nonAscii.Count - 1])
                    {
                        nonAscii[nonAscii.Count - 1] = last;
                    }
                }
                else
                {
                    nonAscii.Add(start);
                    nonAscii.Add(last);
                }
            }
        }

        return (low, high, nonAscii.Count > 0 ? nonAscii.ToArray() : null);
    }

    public string Text { get; }

    /// <summary>
    /// The chars that continue an identifier when they are given as a list, otherwise <see langword="null"/>.
    /// </summary>
    public string? IdentifierPart { get; }

    /// <summary>
    /// The chars that continue an identifier when they are given as ranges, otherwise <see langword="null"/>.
    /// </summary>
    public IReadOnlyList<(char First, char Last)>? IdentifierPartRanges { get; }

    // Whether a custom set is used, instead of the default ASCII letters
    internal bool HasCustomIdentifierPart => IdentifierPart != null || IdentifierPartRanges != null;

    internal bool IsIdentifierPart(char c) => c < 128
        ? (((c < 64 ? _identifierPartLow : _identifierPartHigh) >> (c & 63)) & 1UL) != 0
        : IsNonAsciiIdentifierPart(c);

    internal bool HasSameIdentifierPart(KeywordLiteral other) =>
        _identifierPartLow == other._identifierPartLow && _identifierPartHigh == other._identifierPartHigh
        && (_identifierPartNonAscii ?? []).AsSpan().SequenceEqual(other._identifierPartNonAscii ?? []);

    private bool IsNonAsciiIdentifierPart(char c)
    {
        var ranges = _identifierPartNonAscii;

        if (ranges != null)
        {
            for (var i = 0; i < ranges.Length; i += 2)
            {
                if (c >= ranges[i] && c <= ranges[i + 1])
                {
                    return true;
                }
            }
        }

        return false;
    }

    // A condition on a char variable, with the same bitset as the runtime as constants
    internal string GenerateIsIdentifierPart(string next)
    {
        var nonAscii = "false";

        if (_identifierPartNonAscii != null)
        {
            var ranges = new List<string>();
            for (var i = 0; i < _identifierPartNonAscii.Length; i += 2)
            {
                var first = LiteralHelper.ToLiteral(_identifierPartNonAscii[i]);
                var last = LiteralHelper.ToLiteral(_identifierPartNonAscii[i + 1]);
                ranges.Add(first == last ? $"{next} == {first}" : $"({next} >= {first} && {next} <= {last})");
            }

            nonAscii = string.Join(" || ", ranges);
        }

        return $"({next} < 128 ? (({next} < 64 ? 0x{_identifierPartLow:X}UL : 0x{_identifierPartHigh:X}UL) >> ({next} & 63) & 1UL) != 0 : {nonAscii})";
    }

    public StringComparison Comparison { get; }

    public bool ReturnMatchedText { get; }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; }

    public bool SkipWhitespace { get; }

    // The keyword starts with its text, the boundary is checked after it
    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_textLiteral);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => Text.Length == 0;

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

            // The next char must not continue an identifier (keyword boundary). When the text ends the buffer the
            // cursor reaches the end, which marks HitEnd.
            if (span.Length > length)
            {
                var next = span[length];

                if (next < 128
                    ? (((next < 64 ? _identifierPartLow : _identifierPartHigh) >> (next & 63)) & 1UL) != 0
                    : _identifierPartNonAscii != null && IsNonAsciiIdentifierPart(next))
                {
                    context.ExitParser(this);
                    return false;
                }
            }

            var start = cursor.Offset;

            cursor.AdvanceNoNewLines(length);

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
                        : _textLiteral.Cases!.Get(parsedText));
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
        result.Body.Add($"var {startName} = {cursorName}.Offset;");

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

        if (!HasCustomIdentifierPart)
        {
            result.Body.Add($"    if ({cursorName}.Eof || (!Parlot.Character.IsInRange({cursorName}.Current, 'a', 'z') && !Parlot.Character.IsInRange({cursorName}.Current, 'A', 'Z')))");
        }
        else
        {
            var nextName = $"next{context.NextNumber()}";

            result.Body.Add($"    var {nextName} = {cursorName}.Current;");
            result.Body.Add($"    if ({cursorName}.Eof || !{GenerateIsIdentifierPart(nextName)})");
        }

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
