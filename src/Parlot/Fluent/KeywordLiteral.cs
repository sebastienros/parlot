using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;

namespace Parlot.Fluent;

/// <summary>
/// Parses a keyword: a text that is not followed by a char that continues an identifier. By default these chars
/// are the ASCII letters; a custom set can be given, e.g. to also reject digits, <c>_</c> or <c>-</c>, or the chars
/// above ASCII.
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
        : this(text, identifierPart.ToString(), comparison, returnMatchedText)
    {
    }

    // Keeps the instance of the string, which the keywords of a language share
    internal KeywordLiteral(string text, string identifierPart, StringComparison comparison, bool returnMatchedText)
        : this(text, comparison, returnMatchedText)
    {
        (_identifierPartLow, _identifierPartHigh, _identifierPartNonAscii) = BuildIdentifierPart(identifierPart.AsSpan());
        IdentifierPart = identifierPart;
    }

    // The bitset of the ASCII chars, and the other chars as the first and last chars of sorted ranges, such that a large
    // set, like all the chars above ASCII, is a few comparisons
    private static (ulong Low, ulong High, char[]? NonAscii) BuildIdentifierPart(ReadOnlySpan<char> identifierPart)
    {
        var low = 0UL;
        var high = 0UL;
        var others = new List<char>();

        foreach (var c in identifierPart)
        {
            if (c < 64)
            {
                low |= 1UL << c;
            }
            else if (c < 128)
            {
                high |= 1UL << (c - 64);
            }
            else
            {
                others.Add(c);
            }
        }

        if (others.Count == 0)
        {
            return (low, high, null);
        }

        others.Sort();

        var ranges = new List<char> { others[0], others[0] };

        foreach (var c in others)
        {
            // A char of the last range, or the one after it
            if (c - 1 <= ranges[ranges.Count - 1])
            {
                if (c > ranges[ranges.Count - 1])
                {
                    ranges[ranges.Count - 1] = c;
                }
            }
            else
            {
                ranges.Add(c);
                ranges.Add(c);
            }
        }

        return (low, high, ranges.ToArray());
    }

    public string Text { get; }

    /// <summary>
    /// The chars that continue an identifier, or <see langword="null"/> for the default ASCII letters.
    /// </summary>
    public string? IdentifierPart { get; }

    // Whether a custom set is used, instead of the default ASCII letters
    internal bool HasCustomIdentifierPart => IdentifierPart != null;

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
