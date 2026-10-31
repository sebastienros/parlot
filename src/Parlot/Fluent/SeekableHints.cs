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
    public static (bool CanSeek, char[] ExpectedChars, bool SkipWhitespace) Union<T>(params Parser<T>[] parsers)
    {
        if (parsers.Length == 0 || parsers[0] is not ISeekable first || !first.CanSeek)
        {
            return (false, [], false);
        }

        var skipWhitespace = first.SkipWhitespace;
        var expectedChars = new List<char>();

        foreach (var parser in parsers)
        {
            if (parser is not ISeekable seekable || !seekable.CanSeek || seekable.SkipWhitespace != skipWhitespace)
            {
                return (false, [], false);
            }

            foreach (var c in seekable.ExpectedChars)
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
            && Array.IndexOf(seekable.ExpectedChars, OneOf<object>.OtherSeekableChar) < 0
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
