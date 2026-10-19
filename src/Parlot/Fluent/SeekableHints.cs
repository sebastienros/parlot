using Parlot.Rewriting;
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
}
