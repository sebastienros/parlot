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

    private ForwardedHints(int version, (bool CanSeek, char[] ExpectedChars, bool SkipWhitespace) hints, (object? First, object? Second) prefixes = default)
    {
        _version = version;
        (CanSeek, ExpectedChars, SkipWhitespace) = hints;
        Prefixes = prefixes;
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; }

    public bool SkipWhitespace { get; }

    /// <summary>
    /// Gets the parsers whose prefixes a sequence has, see <see cref="SeekableHints.SequencePrefixes"/>, read with the
    /// hints by <see cref="OfSequence"/> only.
    /// </summary>
    public (object? First, object? Second) Prefixes { get; }

    /// <summary>
    /// Gets the prefixes of a sequence which invokes <paramref name="first"/> then <paramref name="second"/>, see
    /// <see cref="ISeekablePrefixes"/>: those of <paramref name="first"/>, read without the hints, unless it is a lookahead.
    /// </summary>
    public static string[]? SequencePrefixes(ref ForwardedHints? cache, object first, object second)
        => first is ILookahead ? OfSequence(ref cache, first, second).ReadSequencePrefixes() : SeekableHints.Prefixes(first);

    // The prefixes of a sequence which starts with a lookahead, read once for these hints. Two threads can read them at once:
    // they read the same ones.
    private string[]? _sequencePrefixes;
    private bool _sequencePrefixesRead;

    private string[]? ReadSequencePrefixes()
    {
        if (!Volatile.Read(ref _sequencePrefixesRead))
        {
            _sequencePrefixes = SeekableHints.SequencePrefixes(Prefixes);
            Volatile.Write(ref _sequencePrefixesRead, true);
        }

        return _sequencePrefixes;
    }

    /// <summary>
    /// Gets the hints and the prefixes of a sequence which invokes <paramref name="first"/> then <paramref name="second"/>,
    /// see <see cref="SeekableHints.Sequence"/>.
    /// </summary>
    public static ForwardedHints OfSequence(ref ForwardedHints? cache, object first, object second)
        => GetOrCreate(ref cache, (first, second), static (parsers, version) =>
        {
            var hints = SeekableHints.Sequence(parsers.first, parsers.second, out var prefixes);
            return new ForwardedHints(version, hints, prefixes);
        });

    /// <summary>
    /// Gets the hints of <paramref name="parser"/>, the parser that is invoked first.
    /// </summary>
    public static ForwardedHints Get(ref ForwardedHints? cache, object parser) => Get(ref cache, parser, static parser => SeekableHints.Of(parser));

    /// <summary>
    /// Gets the hints <paramref name="read"/> computes from the parsers that can be invoked first.
    /// </summary>
    public static ForwardedHints Get<TState>(ref ForwardedHints? cache, TState state, Func<TState, (bool CanSeek, char[] ExpectedChars, bool SkipWhitespace)> read)
        => GetOrCreate(ref cache, (state, read), static (arguments, version) => new ForwardedHints(version, arguments.read(arguments.state)));

    private static ForwardedHints GetOrCreate<TState>(ref ForwardedHints? cache, TState state, Func<TState, int, ForwardedHints> create)
    {
        // The version is read first, so that hints read while it changes are read again the next time
        var version = SeekableResolution.Version;
        var hints = Volatile.Read(ref cache);

        if (hints == null || hints._version != version)
        {
            hints = create(state, version);
            Volatile.Write(ref cache, hints);
        }

        return hints;
    }
}
