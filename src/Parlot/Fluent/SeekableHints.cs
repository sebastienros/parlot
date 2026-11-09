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
