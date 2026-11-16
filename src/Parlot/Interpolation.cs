using System;
using System.Collections;
using System.Collections.Generic;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif

namespace Parlot;

/// <summary>
/// The value of an interpolated text, like a template literal: the text between its delimiters, as chunks, and the
/// values of the holes between them.
/// </summary>
/// <remarks>
/// <para>
/// A chunk is the raw text: its escape sequences aren't decoded. There is one more chunk than there are holes, so
/// <c>Chunks[i]</c> is the text before <c>Holes[i]</c>, and the last chunk the text after the last hole. A chunk can be empty.
/// The <see langword="default"/> value, which a parser that doesn't match returns, has no chunk and no hole.
/// </para>
/// <para>
/// A text without a hole keeps its chunk in the value, and a text with holes keeps each hole with the chunk before it in a
/// single array: <see cref="Chunks"/> and <see cref="Holes"/> are views of these, which don't allocate unless they are
/// used as interfaces.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the values of the holes.</typeparam>
public readonly struct Interpolation<T>
{
    // Each hole with the chunk before it, or null without a hole
    private readonly (TextSpan Chunk, T Hole)[]? _parts;

    // The chunk after the last hole
    private readonly TextSpan _last;

    // Whether there are chunks: false for the default value
    private readonly bool _hasChunks;

    /// <summary>
    /// Creates an interpolated text from its chunks and the values of its holes.
    /// </summary>
    /// <param name="chunks">The text between the delimiters, one more than <paramref name="holes"/>.</param>
    /// <param name="holes">The values of the holes.</param>
    public Interpolation(IReadOnlyList<TextSpan> chunks, IReadOnlyList<T> holes)
    {
        ThrowHelper.ThrowIfNull(chunks, nameof(chunks));

        ThrowHelper.ThrowIfNull(holes, nameof(holes));

        if (chunks.Count != holes.Count + 1)
        {
            throw new ArgumentException("There must be one more chunk than there are holes.", nameof(chunks));
        }

        if (holes.Count > 0)
        {
            var parts = new (TextSpan Chunk, T Hole)[holes.Count];

            for (var i = 0; i < parts.Length; i++)
            {
                parts[i] = (chunks[i], holes[i]);
            }

            _parts = parts;
        }
        else
        {
            _parts = null;
        }

        _last = chunks[holes.Count];
        _hasChunks = true;
    }

    // A text without a hole
    internal Interpolation(TextSpan chunk)
    {
        _parts = null;
        _last = chunk;
        _hasChunks = true;
    }

    // Each hole with the chunk before it, which the parser sized, and the chunk after the last hole
    internal Interpolation((TextSpan Chunk, T Hole)[] parts, TextSpan last)
    {
        _parts = parts;
        _last = last;
        _hasChunks = true;
    }

    /// <summary>
    /// Creates the value of a text from the holes a parser listed, each with the chunk before it, and the chunk after the
    /// last one. The caller releases the list.
    /// </summary>
    internal static Interpolation<T> Create(ref global::Parlot.Fluent.ListBuilder<(TextSpan Chunk, T Hole)> parts, TextSpan last)
    {
        return new Interpolation<T>(parts.ToArray(), last);
    }

    /// <summary>
    /// Gets the raw text between the delimiters and the holes, one more than <see cref="Holes"/>.
    /// </summary>
    public ChunkList Chunks => new(this);

    /// <summary>
    /// Gets the values of the holes, in the order of the text.
    /// </summary>
    public HoleList Holes => new(this);

    private int HoleCount => _parts?.Length ?? 0;

    /// <summary>
    /// The chunks of an <see cref="Interpolation{T}"/>.
    /// </summary>
    public readonly struct ChunkList : IReadOnlyList<TextSpan>
    {
        private readonly Interpolation<T> _value;

        internal ChunkList(Interpolation<T> value)
        {
            _value = value;
        }

        /// <summary>
        /// Gets the number of chunks, one more than the number of holes.
        /// </summary>
        public int Count => _value._hasChunks ? _value.HoleCount + 1 : 0;

        /// <summary>
        /// Gets the chunk at <paramref name="index"/>.
        /// </summary>
        public TextSpan this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return index == _value.HoleCount ? _value._last : _value._parts![index].Chunk;
            }
        }

        /// <summary>
        /// Returns an enumerator of the chunks.
        /// </summary>
        public Enumerator GetEnumerator() => new(this);

        IEnumerator<TextSpan> IEnumerable<TextSpan>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Enumerates the chunks of an <see cref="Interpolation{T}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<TextSpan>
        {
            private readonly ChunkList _list;
            private int _index;

            internal Enumerator(ChunkList list)
            {
                _list = list;
                _index = -1;
            }

            /// <inheritdoc/>
            public readonly TextSpan Current => _list[_index];

            readonly object IEnumerator.Current => Current;

            /// <inheritdoc/>
            public bool MoveNext() => ++_index < _list.Count;

            /// <inheritdoc/>
            public void Reset() => _index = -1;

            /// <inheritdoc/>
            public readonly void Dispose()
            {
            }
        }
    }

    /// <summary>
    /// The values of the holes of an <see cref="Interpolation{T}"/>.
    /// </summary>
    public readonly struct HoleList : IReadOnlyList<T>
    {
        private readonly Interpolation<T> _value;

        internal HoleList(Interpolation<T> value)
        {
            _value = value;
        }

        /// <summary>
        /// Gets the number of holes.
        /// </summary>
        public int Count => _value.HoleCount;

        /// <summary>
        /// Gets the value of the hole at <paramref name="index"/>.
        /// </summary>
        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return _value._parts![index].Hole;
            }
        }

        /// <summary>
        /// Returns an enumerator of the values of the holes.
        /// </summary>
        public Enumerator GetEnumerator() => new(this);

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Enumerates the values of the holes of an <see cref="Interpolation{T}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly HoleList _list;
            private int _index;

            internal Enumerator(HoleList list)
            {
                _list = list;
                _index = -1;
            }

            /// <inheritdoc/>
            public readonly T Current => _list[_index];

            readonly object? IEnumerator.Current => Current;

            /// <inheritdoc/>
            public bool MoveNext() => ++_index < _list.Count;

            /// <inheritdoc/>
            public void Reset() => _index = -1;

            /// <inheritdoc/>
            public readonly void Dispose()
            {
            }
        }
    }
}

/// <summary>
/// Finds where a chunk of an interpolated text ends, for <c>Interpolated</c> and the code generated for it.
/// </summary>
internal static class InterpolationScanner
{
    /// <summary>The chunk ends with the text's closing delimiter.</summary>
    public const int Close = 0;

    /// <summary>The chunk ends with the opening delimiter of a hole.</summary>
    public const int Hole = 1;

    /// <summary>The chunk holds a new line, which it doesn't accept.</summary>
    public const int NewLine = 2;

    /// <summary>The text ends before the chunk does: more text could end it.</summary>
    public const int End = 3;

    /// <summary>
    /// Returns the length of the chunk at the start of <paramref name="span"/>, and in <paramref name="stop"/> what ends it.
    /// </summary>
    /// <remarks>
    /// The chunk ends before the first <paramref name="close"/> or <paramref name="holeOpen"/> which doesn't follow an
    /// <paramref name="escape"/>, which skips the char after it. With <see cref="NewLine"/>, the length is the offset of the
    /// new line, and with <see cref="End"/> the length of the span.
    /// </remarks>
    /// <param name="span">The text, from the start of the chunk.</param>
    /// <param name="stops">The first chars of <paramref name="close"/> and <paramref name="holeOpen"/>, the escape, and the new lines when they are rejected.</param>
    /// <param name="close">The closing delimiter of the text.</param>
    /// <param name="holeOpen">The opening delimiter of a hole.</param>
    /// <param name="escape">The escape char, or <c>'\0'</c> for none.</param>
    /// <param name="newLines">Whether a chunk can hold a new line.</param>
    /// <param name="stop">What ends the chunk.</param>
#if NET8_0_OR_GREATER
    public static int Scan(ReadOnlySpan<char> span, SearchValues<char> stops, string close, string holeOpen, char escape, bool newLines, out int stop)
#else
    public static int Scan(ReadOnlySpan<char> span, string stops, string close, string holeOpen, char escape, bool newLines, out int stop)
#endif
    {
        var i = 0;

        while (true)
        {
#if NET8_0_OR_GREATER
            var index = span.Slice(i).IndexOfAny(stops);
#else
            var index = span.Slice(i).IndexOfAny(stops.AsSpan());
#endif

            if (index < 0)
            {
                stop = End;
                return span.Length;
            }

            i += index;

            var c = span[i];

            if (c == escape && escape != '\0')
            {
                if (i + 1 >= span.Length)
                {
                    stop = End;
                    return span.Length;
                }

                if (!newLines && span[i + 1] is '\n' or '\r')
                {
                    stop = NewLine;
                    return i + 1;
                }

                i += 2;
                continue;
            }

            var rest = span.Slice(i);

            if (rest.StartsWith(close.AsSpan()))
            {
                stop = Close;
                return i;
            }

            if (rest.StartsWith(holeOpen.AsSpan()))
            {
                stop = Hole;
                return i;
            }

            if (!newLines && c is '\n' or '\r')
            {
                stop = NewLine;
                return i;
            }

            // The first char of a delimiter which doesn't follow, or the start of one which the text ends before
            i++;
        }
    }
}
