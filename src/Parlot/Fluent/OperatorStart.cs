using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Parlot.Fluent;

/// <summary>
/// The chars the operators of some choices of a <see cref="PrecedenceParser{T}"/> can start with, which it tests before
/// invoking them: most operands have no prefix operator, and most are followed by a char no operator starts with, where
/// the choices would fail without reading anything but the white space.
/// </summary>
/// <remarks>
/// The chars are the <see cref="ISeekable"/> hints of the choices, the keys of their lookup tables, read once they are
/// resolved: the char at the cursor for the choices which don't skip the white space, the char after it for the others.
/// </remarks>
internal sealed class OperatorStart
{
    // The chars at the cursor, of the choices which don't skip the white space
    private readonly CharSet _atCursor;

    // The chars after the white space, of the choices which skip it
    private readonly CharSet _afterWhiteSpace;

    // Whether a choice skips the white space, when _afterWhiteSpace has chars
    private readonly bool _skipsWhiteSpace;

    // The chars of the sets, for the generated code, and whether they hold every char above ASCII
    private readonly char[] _atCursorChars;
    private readonly bool _atCursorAnyNonAscii;
    private readonly char[] _afterWhiteSpaceChars;
    private readonly bool _afterWhiteSpaceAnyNonAscii;

    private OperatorStart(List<char> atCursor, bool atCursorAnyNonAscii, List<char> afterWhiteSpace, bool afterWhiteSpaceAnyNonAscii)
    {
        _atCursorChars = atCursor.ToArray();
        _atCursorAnyNonAscii = atCursorAnyNonAscii;
        _afterWhiteSpaceChars = afterWhiteSpace.ToArray();
        _afterWhiteSpaceAnyNonAscii = afterWhiteSpaceAnyNonAscii;

        _atCursor = new CharSet(_atCursorChars, atCursorAnyNonAscii);
        _afterWhiteSpace = new CharSet(_afterWhiteSpaceChars, afterWhiteSpaceAnyNonAscii);
        _skipsWhiteSpace = _afterWhiteSpaceChars.Length > 0 || afterWhiteSpaceAnyNonAscii;
    }

    /// <summary>
    /// Gets the table of <paramref name="choices"/>, once the grammar is complete: when it is parsed or generated. Null
    /// when a choice has no hints, or can start with any char: the choices are then always invoked.
    /// </summary>
    public static OperatorStart? Create(IReadOnlyList<object> choices)
    {
        var atCursor = new List<char>();
        var afterWhiteSpace = new List<char>();
        var atCursorAnyNonAscii = false;
        var afterWhiteSpaceAnyNonAscii = false;

        if (choices.Count == 0)
        {
            return null;
        }

        // The choices build their lookup tables first, which their hints are the keys of, see SeekableHints.Of()
        lock (SeekableResolution.SyncRoot)
        {
            foreach (var choice in choices)
            {
                var (canSeek, expectedChars, skipWhitespace) = SeekableHints.Of(choice);

                if (!canSeek || expectedChars.Length == 0 || Array.IndexOf(expectedChars, OneOf<object>.OtherSeekableChar) >= 0)
                {
                    return null;
                }

                var chars = skipWhitespace ? afterWhiteSpace : atCursor;

                foreach (var c in expectedChars)
                {
                    if (c == OneOf<object>.NonAsciiSeekableChar)
                    {
                        if (skipWhitespace)
                        {
                            afterWhiteSpaceAnyNonAscii = true;
                        }
                        else
                        {
                            atCursorAnyNonAscii = true;
                        }
                    }
                    else if (!chars.Contains(c))
                    {
                        chars.Add(c);
                    }
                }
            }
        }

        return new OperatorStart(atCursor, atCursorAnyNonAscii, afterWhiteSpace, afterWhiteSpaceAnyNonAscii);
    }

    /// <summary>
    /// Whether an operator of the choices can start at the cursor, which stays where it is. When none can, the choices would
    /// fail without reading anything but the white space, and where they would fail is recorded.
    /// </summary>
    /// <remarks>
    /// The choices are always invoked when the input is a reader, whose buffer the white space would be pinned in, or when
    /// the diagnostics are collected.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MayStart(ParseContext context)
    {
        var current = context.Scanner.Cursor.Current;

        if (_atCursor.Contains(current) || context.IsCompactingOrCollectingDiagnostics)
        {
            return true;
        }

        if (!_skipsWhiteSpace)
        {
            return false;
        }

        return context.MaySkipWhiteSpace(current) ? MayStartAfterWhiteSpace(context) : _afterWhiteSpace.Contains(current);
    }

    // The choices skip the white space themselves, keeping what its parser records: CharAfterWhiteSpace() would forget it
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool MayStartAfterWhiteSpace(ParseContext context)
    {
        // The choices skip the white space again from the cache of the context, which doesn't record a failure
        if (_afterWhiteSpace.Contains(context.CharAfterSkippedWhiteSpace(out var recorded)))
        {
            context.Scanner.Cursor.RestoreRecordedFailure(recorded);
            return true;
        }

        // The cursor moved back from the end of the white space, where the choices would fail
        return false;
    }

    /// <summary>
    /// Emits the local function <paramref name="name"/> which returns whether an operator can start at the cursor, like
    /// <see cref="MayStart(ParseContext)"/>. Not when the input is a reader, or when the diagnostics are collected.
    /// </summary>
    public string GenerateTest(string name, string ctx, string cursor)
    {
        var sb = new StringBuilder();

        void Line(string text) => sb.Append(text).Append('\n');

        Line($"bool {name}()");
        Line("{");
        Line($"    var operatorChar = {cursor}.Current;");

        if (_atCursorChars.Length > 0 || _atCursorAnyNonAscii)
        {
            Line($"    if ({Test("operatorChar", _atCursorChars, _atCursorAnyNonAscii)}) return true;");
        }

        if (!_skipsWhiteSpace)
        {
            Line("    return false;");
        }
        else
        {
            var after = Test("operatorChar", _afterWhiteSpaceChars, _afterWhiteSpaceAnyNonAscii);
            Line($"    if (!{ctx}.MaySkipWhiteSpace(operatorChar)) return {after};");

            // The cursor moved back from the end of the white space, where the choices would fail when none can start there
            Line($"    operatorChar = {ctx}.CharAfterSkippedWhiteSpace(out var operatorFailure);");
            Line($"    if (!({after})) return false;");
            Line($"    {cursor}.RestoreRecordedFailure(operatorFailure);");
            Line("    return true;");
        }

        sb.Append('}');
        return sb.ToString();
    }

    // A C# expression which tests whether the char value is one of chars, or above ASCII when anyNonAscii is set
    private static string Test(string value, char[] chars, bool anyNonAscii)
    {
        var patterns = new List<string>();
        var listed = new List<char>();

        foreach (var c in chars)
        {
            if (c < 128 || !anyNonAscii)
            {
                listed.Add(c);
            }
        }

        if (listed.Count > 0)
        {
            patterns.Add(OneOf<object>.BuildCharPattern(listed, rangeThreshold: 4));
        }

        if (anyNonAscii)
        {
            patterns.Add(">= '\\u0080'");
        }

        return $"{value} is {string.Join(" or ", patterns)}";
    }
}
