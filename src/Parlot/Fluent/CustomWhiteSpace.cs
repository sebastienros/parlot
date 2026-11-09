using Parlot.Rewriting;
using System;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif

namespace Parlot.Fluent;

/// <summary>
/// A <see cref="ParseContext.WhiteSpaceParser"/>, with the chars before which it can read something, from its seek hints. The
/// parsers skipping white space don't invoke it before the other chars, the shortcut the white space sets have with their char
/// table.
/// </summary>
/// <remarks>
/// Skipping white space ignores the result of the parser, and only uses where it leaves the cursor. Not invoking it where it
/// would read nothing is then the same, but for the parsers it would enter (<see cref="ParseContext.OnEnterParser"/>). The
/// context keeps this instead of the parser, so that it doesn't grow, and one is created for each parser.
/// </remarks>
internal sealed class CustomWhiteSpace
{
    // How many parsers that read only with the parser they wrap are looked through, see IReadsWith
    private const int MaxDepth = 8;

    // A white space parser is set on each context, which is created for each parse, and by WithWhiteSpaceParser for each parse
    private static readonly ConditionalWeakTable<Parser<TextSpan>, CustomWhiteSpace> s_cache = new();

    // The ASCII chars, a bit each
    private readonly ulong _low;
    private readonly ulong _high;

    // Whether it can start with any char above ASCII, when it doesn't tell its chars or declares OneOf<T>.NonAsciiSeekableChar
    private readonly bool _anyOther;

    // The chars above ASCII, null when there are none
#if NET8_0_OR_GREATER
    private readonly SearchValues<char>? _others;
#else
    private readonly char[]? _others;
#endif

    private CustomWhiteSpace(Parser<TextSpan> parser)
    {
        Parser = parser;

        var chars = GetStartChars(parser);

        if (chars is null)
        {
            // The parser is invoked before any char
            _low = ulong.MaxValue;
            _high = ulong.MaxValue;
            _anyOther = true;
            return;
        }

        var others = 0;

        foreach (var c in chars)
        {
            if (c < 64)
            {
                _low |= 1UL << c;
            }
            else if (c < 128)
            {
                _high |= 1UL << (c - 64);
            }
            else if (c == OneOf<object>.NonAsciiSeekableChar)
            {
                // Not the char U+FFFF, but any char above ASCII, as in the lookup table of a choice
                _anyOther = true;
            }
            else
            {
                others++;
            }
        }

        // The end of the text is NUL: the parser is still invoked there, where a stream tells whether more text follows
        _low |= 1UL;

        if (others > 0 && !_anyOther)
        {
            var otherChars = new char[others];
            var i = 0;

            foreach (var c in chars)
            {
                if (c >= 128 && c != OneOf<object>.NonAsciiSeekableChar)
                {
                    otherChars[i++] = c;
                }
            }

#if NET8_0_OR_GREATER
            _others = SearchValues.Create(otherChars);
#else
            Array.Sort(otherChars);
            _others = otherChars;
#endif
        }
    }

    /// <summary>
    /// The white space parser.
    /// </summary>
    public Parser<TextSpan> Parser { get; }

    /// <summary>
    /// Gets the instance of <paramref name="parser"/>, or <c>null</c> for <c>null</c>.
    /// </summary>
    /// <remarks>The grammar must be complete, as when it parses: the hints of a <see cref="Deferred{T}"/> are read.</remarks>
    public static CustomWhiteSpace? Of(Parser<TextSpan>? parser) => parser is null ? null : s_cache.GetValue(parser, static p => new CustomWhiteSpace(p));

    /// <summary>
    /// Reads the chars <paramref name="parser"/> can read something before, or <c>null</c> when it doesn't tell them.
    /// </summary>
    private static char[]? GetStartChars(object parser)
    {
        for (var depth = 0; depth < MaxDepth; depth++)
        {
            var (canSeek, expectedChars, skipWhitespace) = SeekableHints.Of(parser);

            // A parser that skips white space itself can start with any of them. One that can start with any char declares
            // OtherSeekableChar.
            if (canSeek && !skipWhitespace && expectedChars.Length > 0 && Array.IndexOf(expectedChars, OneOf<object>.OtherSeekableChar) < 0)
            {
                return expectedChars;
            }

            // A parser that can succeed without reading has no hints, like the ZeroOrMany of WithComments(), but it reads
            // only where the parser it starts with does
            if (canSeek || parser is not IReadsWith wrapper)
            {
                return null;
            }

            parser = wrapper.Parser;
        }

        return null;
    }

    /// <summary>
    /// Whether the parser can read something before <paramref name="c"/>. It is <c>true</c> for NUL, the
    /// <see cref="Cursor.Current"/> char at the end of the text.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MayStartWith(char c)
    {
        if (c < 64)
        {
            return ((_low >> c) & 1) != 0;
        }

        if (c < 128)
        {
            return ((_high >> (c - 64)) & 1) != 0;
        }

        return _anyOther || (_others is not null && MayStartWithOther(c));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool MayStartWithOther(char c)
    {
#if NET8_0_OR_GREATER
        return _others!.Contains(c);
#else
        return Array.BinarySearch(_others!, c) >= 0;
#endif
    }
}

/// <summary>
/// A parser that moves the cursor only when <see cref="Parser"/>, which it invokes first at its position, does: where that
/// parser reads nothing, this one reads nothing either, whether it succeeds or not.
/// </summary>
/// <remarks>
/// <see cref="CustomWhiteSpace"/> looks through such a parser when it can succeed without reading, so has no seek hints, like
/// <c>Capture(ZeroOrMany(comment))</c>.
/// </remarks>
internal interface IReadsWith
{
    object Parser { get; }
}
