using Parlot.Rewriting;
using System;
using System.Threading;

namespace Parlot.Fluent;

/// <summary>
/// The <see cref="ISeekable"/> hints of a parser that reads them from the parsers it can invoke first.
/// </summary>
/// <remarks>
/// <para>
/// Such a parser can be built before a <see cref="Deferred{T}"/> parser it starts with is set, or before a choice it starts
/// with builds its complete lookup table, so it reads the hints when they are asked for rather than when it is built. The hints
/// are only asked for when a grammar is built and when its choices build their lookup tables, never while it parses.
/// </para>
/// <para>
/// The hints are kept until <see cref="SeekableResolution.Version"/> changes, such that reading the hints of a grammar, where
/// parsers read the hints of the parsers they start with, takes a time linear in its size.
/// </para>
/// </remarks>
internal sealed class ForwardedHints
{
    private readonly int _version;

    private ForwardedHints(int version, (bool CanSeek, char[] ExpectedChars, bool SkipWhitespace) hints)
    {
        _version = version;
        (CanSeek, ExpectedChars, SkipWhitespace) = hints;
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; }

    public bool SkipWhitespace { get; }

    /// <summary>
    /// Gets the hints of <paramref name="parser"/>, the parser that is invoked first.
    /// </summary>
    public static ForwardedHints Get(ref ForwardedHints? cache, object parser) => Get(ref cache, parser, static parser => SeekableHints.Of(parser));

    /// <summary>
    /// Gets the hints <paramref name="read"/> computes from the parsers that can be invoked first.
    /// </summary>
    public static ForwardedHints Get<TState>(ref ForwardedHints? cache, TState state, Func<TState, (bool CanSeek, char[] ExpectedChars, bool SkipWhitespace)> read)
    {
        // The version is read first, so that hints read while it changes are read again the next time
        var version = SeekableResolution.Version;
        var hints = Volatile.Read(ref cache);

        if (hints == null || hints._version != version)
        {
            hints = new ForwardedHints(version, read(state));
            Volatile.Write(ref cache, hints);
        }

        return hints;
    }
}
