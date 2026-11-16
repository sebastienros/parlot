using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Parlot.Fluent;

/// <summary>
/// The chars the operators of some choices of a <see cref="PrecedenceParser{T}"/> can start with, which it tests before
/// invoking them: most operands have no prefix operator, and most are followed by a char no operator starts with, where
/// the choices would fail without reading anything.
/// </summary>
/// <remarks>
/// The chars are the <see cref="ISeekable"/> hints of the choices, the keys of their lookup tables, read once they are
/// resolved. The choices which skip the white space are invoked when there is some to skip, or when the char at the cursor
/// is one of theirs.
/// </remarks>
internal sealed class OperatorStart
{
    // The chars the operators of the choices start with, at the cursor: the ones of the choices which don't skip the white
    // space, and of the ones which do, when there is none
    private readonly CharSet _set;

    // The chars of the set, for the generated code, and whether it holds every char above ASCII
    private readonly char[] _chars;
    private readonly bool _anyNonAscii;

    // Whether a choice skips the white space
    private readonly bool _skipsWhiteSpace;

    private OperatorStart(List<char> chars, bool anyNonAscii, bool skipsWhiteSpace)
    {
        _chars = chars.ToArray();
        _anyNonAscii = anyNonAscii;
        _set = new CharSet(_chars, anyNonAscii);
        _skipsWhiteSpace = skipsWhiteSpace;
    }

    /// <summary>
    /// Gets the table of <paramref name="choices"/>, once the grammar is complete: when it is parsed or generated. Null
    /// when a choice has no hints, or can start with any char: the choices are then always invoked.
    /// </summary>
    public static OperatorStart? Create(IReadOnlyList<object> choices)
    {
        var chars = new List<char>();
        var anyNonAscii = false;
        var skipsWhiteSpace = false;

        if (choices.Count == 0)
        {
            return null;
        }

        // The choices build their lookup tables first, which their hints are the keys of, see SeekableHints.Of(). The hints of a
        // choice which can start with any char are null.
        lock (SeekableResolution.SyncRoot)
        {
            foreach (var choice in choices)
            {
                if (SeekGuard.SeekableHintsOf(choice) is not { } hints)
                {
                    return null;
                }

                var (expectedChars, skipWhitespace) = hints;

                skipsWhiteSpace |= skipWhitespace;

                foreach (var c in expectedChars)
                {
                    if (c == OneOf<object>.NonAsciiSeekableChar)
                    {
                        anyNonAscii = true;
                    }
                    else if (!chars.Contains(c))
                    {
                        chars.Add(c);
                    }
                }
            }
        }

        if (skipsWhiteSpace)
        {
            // The ASCII white space, which an operator that skips it can follow: in the set, the char after an operand is
            // tested once when it is a space. A set of the context which doesn't skip one of them invokes the choices for
            // nothing, which then fail as they would.
            foreach (var c in " \t\n\r\v\f")
            {
                if (!chars.Contains(c))
                {
                    chars.Add(c);
                }
            }
        }

        return new OperatorStart(chars, anyNonAscii, skipsWhiteSpace);
    }

    /// <summary>
    /// Whether an operator of the choices can start at the cursor, which stays where it is. When none can, the choices would
    /// fail without reading anything, and would record no failure.
    /// </summary>
    /// <remarks>
    /// The choices which skip the white space are invoked when there is some to skip: reading the char after it, and moving
    /// back, cost more than they do (<c>ValuedLevelsBenchmarks</c> 9-19% on its input of spaced operators). They are always
    /// invoked when the input is a reader or when the diagnostics are collected.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MayStart(ParseContext context)
    {
        var current = context.Scanner.Cursor.Current;

        return _set.Contains(current)
            || (_skipsWhiteSpace && context.MaySkipWhiteSpace(current))
            || context.IsCompactingOrCollectingDiagnostics;
    }

    /// <summary>
    /// Gets the C# expression which tells whether an operator can start at the cursor, like
    /// <see cref="MayStart(ParseContext)"/>. Not when the input is a reader, or when the diagnostics are collected.
    /// </summary>
    public string GenerateTest(SourceGenerationContext context, string cursor)
    {
        var chars = _anyNonAscii ? [.. _chars, OneOf<object>.NonAsciiSeekableChar] : _chars;
        var test = SeekGuard.GenerateTest(context, $"{cursor}.Current", chars);

        return _skipsWhiteSpace ? $"(({test}) || {context.ParseContextName}.MaySkipWhiteSpace({cursor}.Current))" : $"({test})";
    }
}
