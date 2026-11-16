using Parlot.Rewriting;
using System;
using System.Collections.Generic;

namespace Parlot.Fluent;

/// <summary>
/// Combines the <see cref="ISeekable"/> hints of parsers which can each be the one that reads first.
/// </summary>
internal static class SeekableHints
{
    /// <summary>
    /// Gets the hints of a parser which runs any one of the <paramref name="parsers"/>, like the branches of a conditional parser.
    /// </summary>
    /// <remarks>
    /// The hints are only usable when every parser can seek and they all skip white space the same way,
    /// otherwise the resulting parser can start with any char.
    /// </remarks>
    public static (bool CanSeek, char[] ExpectedChars, bool SkipWhitespace) Union<T>(params Parser<T>[] parsers) => Union((IReadOnlyList<object>)parsers);

    /// <summary>
    /// Gets the hints of a parser which runs any one of the <paramref name="parsers"/>, whatever their value types.
    /// </summary>
    public static (bool CanSeek, char[] ExpectedChars, bool SkipWhitespace) Union(IReadOnlyList<object> parsers)
    {
        if (parsers.Count == 0)
        {
            return (false, [], false);
        }

        var skipWhitespace = false;
        var expectedChars = new List<char>();

        for (var i = 0; i < parsers.Count; i++)
        {
            var hints = Of(parsers[i]);

            if (i == 0)
            {
                skipWhitespace = hints.SkipWhitespace;
            }

            if (!hints.CanSeek || hints.SkipWhitespace != skipWhitespace)
            {
                return (false, [], false);
            }

            foreach (var c in hints.ExpectedChars)
            {
                if (!expectedChars.Contains(c))
                {
                    expectedChars.Add(c);
                }
            }
        }

        return (true, expectedChars.ToArray(), skipWhitespace);
    }

    /// <summary>
    /// Gets the hints of a sequence which invokes <paramref name="first"/> then <paramref name="second"/>: those of
    /// <paramref name="first"/>, or, when it is a <see cref="Not{T}"/>, which succeeds without reading, those of both its
    /// parser and <paramref name="second"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// After a <c>Not</c>, the sequence only matches where <paramref name="second"/> matches, at the same position. Its
    /// parser is invoked first though: where it can start, it can read further and fail with an error, like a parser of
    /// <c>ElseError</c>, which skipping the sequence would hide. Where neither can start, the parser fails at once and so
    /// does the sequence. After a <see cref="Peek{T}"/>, whose parser must match too, the sequence has its hints.
    /// </para>
    /// <para>
    /// A choice whose alternatives all skip white space skips it before it invokes them, which mustn't change what either
    /// parser sees: hints which skip white space are only kept when both parsers tell that they skip it. Whether a parser
    /// skips it is read from its hint, as a choice does. A parser whose match depends on that white space, like
    /// <see cref="NoNewLineBefore{T}"/>, is found by the choice through the lookahead, which then invokes its alternatives
    /// before the white space, see <see cref="IReadsLeadingWhiteSpace"/>.
    /// </para>
    /// </remarks>
    /// <param name="first">The parser the sequence invokes first.</param>
    /// <param name="second">The parser it invokes then.</param>
    /// <param name="prefixes">The parsers whose prefixes the sequence has, see <see cref="SequencePrefixes"/>: none when
    /// it has no hints.</param>
    public static (bool CanSeek, char[] ExpectedChars, bool SkipWhitespace) Sequence(object first, object second, out (object? First, object? Second) prefixes)
    {
        // The prefixes of another first parser are read from it, see ForwardedHints.SequencePrefixes
        prefixes = (null, null);

        if (first is not ILookahead lookahead)
        {
            return Of(first);
        }

        var parser = Of(lookahead.Parser);
        var next = Of(second);

        if (lookahead.MatchesParser)
        {
            // The parser of the lookahead is the one which reads first: where it can't start, it fails at once. Its hints are
            // those the Peek forwards, unless they skip white space and the parser which follows tells that it doesn't. A
            // parser which tells nothing is trusted, as a choice trusts it.
            if (!parser.SkipWhitespace || second is not ISeekable { SkipWhitespace: false })
            {
                prefixes = parser.CanSeek ? (lookahead.Parser, null) : (null, null);
                return parser;
            }

            return (false, [], false);
        }
        else if (parser.CanSeek && next.CanSeek && parser.SkipWhitespace == next.SkipWhitespace)
        {
            prefixes = (lookahead.Parser, second);

            // The hints of both are already read: Union() would read them again
            return (true, ExpectedChars([.. parser.ExpectedChars, .. next.ExpectedChars]), parser.SkipWhitespace);
        }

        return (false, [], false);
    }

    /// <summary>
    /// Gets the prefixes of a sequence, from the parsers <see cref="Sequence"/> tells: the prefixes of both when it tells
    /// two, which are only usable when both tell them. They are read when they are asked for, like the prefixes of other
    /// parsers, rather than with the hints.
    /// </summary>
    public static string[]? SequencePrefixes((object? First, object? Second) prefixes)
    {
        if (prefixes.First is null || Prefixes(prefixes.First) is not { } first)
        {
            return null;
        }

        if (prefixes.Second is null)
        {
            return first;
        }

        return Prefixes(prefixes.Second) is { } second ? [.. first, .. second] : null;
    }

    /// <summary>
    /// Gets the hints of a parser, or none when it doesn't tell them.
    /// </summary>
    /// <remarks>
    /// While the hints of a grammar are resolved, a choice whose lookup table isn't complete yet builds it first, such that
    /// the parsers that start with the choice forward the hints of the complete table, see <see cref="IResolvableSeekable"/>.
    /// </remarks>
    public static (bool CanSeek, char[] ExpectedChars, bool SkipWhitespace) Of(object parser)
    {
        if (parser is not ISeekable seekable)
        {
            return (false, [], false);
        }

        if (parser is IResolvableSeekable resolvable && SeekableResolution.IsResolving)
        {
            resolvable.EnsureResolved();
        }

        return (seekable.CanSeek, seekable.ExpectedChars, seekable.SkipWhitespace);
    }

    /// <summary>
    /// The most non-ASCII chars a parser lists as the chars it can start with. A larger set is listed as
    /// <see cref="OneOf{T}.NonAsciiSeekableChar"/>, so that each lookup table holding the parser doesn't build an entry for
    /// each of its chars.
    /// </summary>
    public const int MaxNonAsciiChars = 64;

    /// <summary>
    /// Gets whether <paramref name="expectedChars"/> are all the chars a parser can start with, rather than declaring it can
    /// start with any char, or with any non-ASCII char.
    /// </summary>
    public static bool IsExact(char[] expectedChars) =>
        Array.IndexOf(expectedChars, OneOf<object>.OtherSeekableChar) < 0 && Array.IndexOf(expectedChars, OneOf<object>.NonAsciiSeekableChar) < 0;

    /// <summary>
    /// Gets the chars a parser declares it can start with: <paramref name="chars"/> once each, or, when they hold more than
    /// <see cref="MaxNonAsciiChars"/> non-ASCII chars, their ASCII chars and <see cref="OneOf{T}.NonAsciiSeekableChar"/>.
    /// </summary>
    public static char[] ExpectedChars(ReadOnlySpan<char> chars)
    {
        var ascii = new List<char>();
        var nonAscii = new HashSet<char>();

        foreach (var c in chars)
        {
            if (c < 0x80)
            {
                if (!ascii.Contains(c))
                {
                    ascii.Add(c);
                }
            }
            else
            {
                nonAscii.Add(c);
            }
        }

        if (nonAscii.Count > MaxNonAsciiChars)
        {
            ascii.Add(OneOf<object>.NonAsciiSeekableChar);
            return ascii.ToArray();
        }

        // The chars keep their order, like before they were compacted
        var result = new List<char>(ascii.Count + nonAscii.Count);

        foreach (var c in chars)
        {
            if (!result.Contains(c))
            {
                result.Add(c);
            }
        }

        return result.ToArray();
    }

    /// <summary>
    /// Gets the prefixes a parser forwards from the parser which reads first, see <see cref="ISeekablePrefixes"/>.
    /// </summary>
    public static string[]? Prefixes(object parser) => parser is ISeekablePrefixes seekable ? seekable.ExpectedPrefixes : null;

    /// <summary>
    /// Gets the texts the input starts with when <paramref name="parser"/> matches it from <paramref name="c"/>. It is only
    /// <paramref name="c"/> when the parser can't tell more, or when it can start with any char.
    /// </summary>
    /// <remarks>
    /// A choice only invokes the parser for the chars it expects, so the prefixes which start with other chars are ignored,
    /// as are the ones a shorter prefix already covers.
    /// </remarks>
    public static List<string> Prefixes(object parser, char c)
    {
        var result = new List<string>();

        if (parser is ISeekable { CanSeek: true } seekable
            && IsExact(seekable.ExpectedChars)
            && Prefixes(parser) is { } prefixes)
        {
            foreach (var prefix in prefixes)
            {
                if (prefix.Length > 0 && prefix[0] == c && !Array.Exists(prefixes, other => other.Length > 0
                    && other.Length < prefix.Length && prefix.StartsWith(other, StringComparison.Ordinal)) && !result.Contains(prefix))
                {
                    result.Add(prefix);
                }
            }
        }

        if (result.Count == 0)
        {
            result.Add(c.ToString());
        }

        return result;
    }
}
