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
/// When a choice has no hints, or can start with any char, the table is <see cref="Any"/>, which lets every char through.
/// </remarks>
internal sealed class OperatorStart
{
    /// <summary>
    /// The table of choices which can start with any char, which are always invoked.
    /// </summary>
    public static readonly OperatorStart Any = new(CharSet.All, default, skipsWhiteSpace: false);

    // The chars at the cursor, of the choices which don't skip the white space
    private readonly CharSet _atCursor;

    // The chars after the white space, of the choices which skip it
    private readonly CharSet _afterWhiteSpace;

    // Whether a choice skips the white space
    private readonly bool _skipsWhiteSpace;

    private OperatorStart(CharSet atCursor, CharSet afterWhiteSpace, bool skipsWhiteSpace)
    {
        _atCursor = atCursor;
        _afterWhiteSpace = afterWhiteSpace;
        _skipsWhiteSpace = skipsWhiteSpace;
    }

    /// <summary>
    /// Gets the table of <paramref name="choices"/>, once the grammar is complete: when it is parsed or generated.
    /// </summary>
    public static OperatorStart Create(IReadOnlyList<object> choices)
    {
        var atCursor = new CharSetBuilder();
        var afterWhiteSpace = new CharSetBuilder();
        var skipsWhiteSpace = false;

        // The choices build their lookup tables first, which their hints are the keys of, see SeekableHints.Of()
        lock (SeekableResolution.SyncRoot)
        {
            foreach (var choice in choices)
            {
                var (canSeek, expectedChars, skipWhitespace) = SeekableHints.Of(choice);

                if (!canSeek || expectedChars.Length == 0 || Array.IndexOf(expectedChars, OneOf<object>.OtherSeekableChar) >= 0)
                {
                    return Any;
                }

                (skipWhitespace ? afterWhiteSpace : atCursor).Add(expectedChars);
                skipsWhiteSpace |= skipWhitespace;
            }
        }

        return new OperatorStart(atCursor.Build(), afterWhiteSpace.Build(), skipsWhiteSpace);
    }

    /// <summary>
    /// Whether the table lets every char through.
    /// </summary>
    public bool IsAny => ReferenceEquals(this, Any);

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool MayStartAfterWhiteSpace(ParseContext context)
    {
        // The choices skip the white space from the cache of the context, which doesn't record a failure again
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
    /// <see cref="MayStart(ParseContext)"/>. Not for a table which is <see cref="Any"/>, or when the input is a reader.
    /// </summary>
    public string GenerateTest(string name, string ctx, string cursor)
    {
        var sb = new StringBuilder();

        void Line(string text) => sb.Append(text).Append('\n');

        Line($"bool {name}()");
        Line("{");
        Line($"    var operatorChar = {cursor}.Current;");

        if (!_atCursor.IsEmpty)
        {
            Line($"    if ({_atCursor.Test("operatorChar")}) return true;");
        }

        if (!_skipsWhiteSpace)
        {
            Line("    return false;");
        }
        else
        {
            Line($"    if (!{ctx}.MaySkipWhiteSpace(operatorChar)) return {_afterWhiteSpace.Test("operatorChar")};");

            // The cursor moved back from the end of the white space, where the choices would fail when none can start there
            Line($"    if (!({_afterWhiteSpace.Test($"{ctx}.CharAfterSkippedWhiteSpace(out var operatorFailure)")})) return false;");
            Line($"    {cursor}.RestoreRecordedFailure(operatorFailure);");
            Line("    return true;");
        }

        sb.Append('}');
        return sb.ToString();
    }

    // The chars of a table: a mask of the ASCII ones, and the non-ASCII ones
    private readonly struct CharSet
    {
        public static readonly CharSet All = new(ulong.MaxValue, ulong.MaxValue, anyNonAscii: true, []);

        private readonly ulong _low;
        private readonly ulong _high;
        private readonly bool _anyNonAscii;
        private readonly char[] _nonAscii;

        public CharSet(ulong low, ulong high, bool anyNonAscii, char[] nonAscii)
        {
            _low = low;
            _high = high;
            _anyNonAscii = anyNonAscii;
            _nonAscii = nonAscii;
        }

        public bool IsEmpty => _low == 0 && _high == 0 && !_anyNonAscii && (_nonAscii is null || _nonAscii.Length == 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(char c) => c < 64
            ? ((_low >> c) & 1) != 0
            : c < 128 ? ((_high >> (c - 64)) & 1) != 0 : ContainsNonAscii(c);

        [MethodImpl(MethodImplOptions.NoInlining)]
        private bool ContainsNonAscii(char c) => _anyNonAscii || (_nonAscii is not null && Array.IndexOf(_nonAscii, c) >= 0);

        // A C# expression which tests whether the char value is in the set
        public string Test(string value)
        {
            var chars = new List<char>();

            for (var c = 0; c < 128; c++)
            {
                if (Contains((char)c))
                {
                    chars.Add((char)c);
                }
            }

            if (!_anyNonAscii && _nonAscii is not null)
            {
                chars.AddRange(_nonAscii);
            }

            var patterns = new List<string>();

            if (chars.Count > 0)
            {
                patterns.Add(OneOf<object>.BuildCharPattern(chars, rangeThreshold: 4));
            }

            if (_anyNonAscii)
            {
                patterns.Add(">= '\\u0080'");
            }

            return patterns.Count == 0 ? "false" : $"{value} is {string.Join(" or ", patterns)}";
        }
    }

    private sealed class CharSetBuilder
    {
        private ulong _low;
        private ulong _high;
        private bool _anyNonAscii;
        private readonly List<char> _nonAscii = [];

        public void Add(char[] chars)
        {
            foreach (var c in chars)
            {
                if (c == OneOf<object>.NonAsciiSeekableChar)
                {
                    _anyNonAscii = true;
                }
                else if (c < 64)
                {
                    _low |= 1UL << c;
                }
                else if (c < 128)
                {
                    _high |= 1UL << (c - 64);
                }
                else if (!_nonAscii.Contains(c))
                {
                    _nonAscii.Add(c);
                }
            }
        }

        public CharSet Build() => new(_low, _high, _anyNonAscii, _anyNonAscii ? [] : _nonAscii.ToArray());
    }
}
