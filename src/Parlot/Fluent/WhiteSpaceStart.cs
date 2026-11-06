using Parlot.Rewriting;
using System;
using System.Runtime.CompilerServices;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif

namespace Parlot.Fluent;

/// <summary>
/// The chars a white space parser can start with, from its seek hints, such that it isn't invoked at the other chars.
/// </summary>
internal sealed class WhiteSpaceStart
{
    // The parsers whose hints were already read: a white space parser is set each time a WithWhiteSpaceParser parses
    private static readonly ConditionalWeakTable<object, WhiteSpaceStart> s_cache = new();

    private static readonly WhiteSpaceStart s_none = new([]);

#if NET8_0_OR_GREATER
    private readonly SearchValues<char>? _chars;
#else
    private readonly bool[]? _ascii;
    private readonly char[]? _others;
#endif

    private WhiteSpaceStart(char[] chars)
    {
#if NET8_0_OR_GREATER
        _chars = chars.Length == 0 ? null : SearchValues.Create(chars);
#else
        _ascii = new bool[128];
        var others = new System.Collections.Generic.List<char>();
        foreach (var c in chars)
        {
            if (c < 128)
            {
                _ascii[c] = true;
            }
            else
            {
                others.Add(c);
            }
        }
        _others = others.ToArray();
#endif
    }

    /// <summary>
    /// The start chars of <paramref name="parser"/>, or <c>null</c> when it can't tell them.
    /// </summary>
    public static WhiteSpaceStart? Of(Parser<TextSpan>? parser)
    {
        if (parser is null)
        {
            return null;
        }

        var start = s_cache.GetValue(parser, static p =>
        {
            // A parser which skips white space itself, or can start with any char, is always invoked
            if (p is ISeekable { CanSeek: true, SkipWhitespace: false } seekable
                && seekable.ExpectedChars.Length > 0
                && Array.IndexOf(seekable.ExpectedChars, OneOf<TextSpan>.OtherSeekableChar) < 0)
            {
                return new WhiteSpaceStart(seekable.ExpectedChars);
            }

            return s_none;
        });

        return ReferenceEquals(start, s_none) ? null : start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(char c)
    {
#if NET8_0_OR_GREATER
        return _chars!.Contains(c);
#else
        return c < 128 ? _ascii![c] : Array.IndexOf(_others!, c) >= 0;
#endif
    }
}
