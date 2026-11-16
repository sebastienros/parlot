using System;
using System.Runtime.CompilerServices;

namespace Parlot;

public class Cursor
{
    public const char NullChar = '\0';

    private int _textLength;

    // Index of the current char in Buffer. Offset adds _base, the absolute offset of Buffer[0], which is 0 unless
    // the cursor reads a compacting window of a stream (see ReplaceBuffer).
    private int _offset;
#if PARLOT_STRING_ONLY
    // Generated parsers without a TextReader entry point only parse strings: a constant base keeps the offset arithmetic out of their hot paths.
    private const int _base = 0;
#else
    private int _base;
#endif

    // The line and column are only computed when a position is requested, from the text (see GetPosition), such that moving
    // the cursor only updates the offset. Absolute offsets. The origin is the position the cursor was created at, the only
    // one whose line and column are given rather than counted. In compacting mode it moves to the start of the window when
    // the text before it is dropped. Invariant: _base <= _originOffset <= _base + _textLength.
    private int _originOffset;
    private int _originLine;
    private int _originColumn;

    // The line terminators, null for the default "\n", and the starts of the lines counted after the origin, created when
    // a position is first requested such that a parse which doesn't request positions doesn't allocate it
    private string? _newLines;
    private LineMap? _lines;

    // The furthest absolute offset the cursor was moved back from, see FurthestOffset
    private int _furthestOffset;

    // HitEndFlag is set unconditionally on the cold end-of-buffer paths; only observable through HitEnd when the buffer is not final.
    // IsFinal shares the byte so that the cursor, which is allocated for each parse, doesn't grow.
    private byte _flags;
    private const byte HitEndFlag = 1;
    private const byte NotFinalFlag = 2;

    // Set on a final buffer only, which HitEnd ignores, see MarkFollowedByLineFeed
    private const byte FollowedByLineFeedFlag = 4;

    /// <summary>
    /// Creates a cursor over <paramref name="buffer"/> starting at <paramref name="position"/>.
    /// </summary>
    /// <param name="buffer">The text to read.</param>
    /// <param name="position">The initial position. Its offset indexes into <paramref name="buffer"/>, its line and column are reported as-is.</param>
    public Cursor(string buffer, in TextPosition position) : this(buffer, position, isFinal: true)
    {
    }

    /// <summary>
    /// Creates a cursor over <paramref name="buffer"/> starting at <paramref name="position"/>.
    /// </summary>
    /// <param name="buffer">The text to read.</param>
    /// <param name="position">The initial position. Its offset indexes into <paramref name="buffer"/>, its line and column are reported as-is.</param>
    /// <param name="isFinal">
    /// <see langword="false"/> when more text may follow <paramref name="buffer"/>, as when parsing a stream.
    /// The cursor then records in <see cref="HitEnd"/> whether a decision depended on the end of the buffer.
    /// </param>
    public Cursor(string buffer, in TextPosition position, bool isFinal)
    {
        Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        _textLength = Buffer.Length;

        if ((uint)position.Offset > (uint)_textLength)
        {
            throw new ArgumentOutOfRangeException(nameof(position));
        }

        _offset = position.Offset;
        _originOffset = _furthestOffset = position.Offset;
        _originLine = position.Line;
        _originColumn = position.Column;
        Eof = _offset == _textLength;
        _flags = (byte)((isFinal ? 0 : NotFinalFlag) | (Eof ? HitEndFlag : 0));
        Current = Eof ? NullChar : Buffer[_offset];
    }

    /// <summary>
    /// Whether <see cref="Buffer"/> holds the end of the input. <see langword="true"/> unless the cursor was created
    /// for a window of a larger input, as when parsing a stream.
    /// </summary>
    public bool IsFinal => (_flags & NotFinalFlag) == 0;

    /// <summary>
    /// Whether, since the cursor was created, a read reached or looked past the end of a non-final <see cref="Buffer"/>.
    /// A parse result obtained while this is <see langword="true"/> may change once more text is available.
    /// Always <see langword="false"/> when <see cref="IsFinal"/> is <see langword="true"/>.
    /// </summary>
    /// <remarks>The flag is sticky: moving the cursor back with <see cref="ResetPosition(int)"/> does not clear it.</remarks>
    public bool HitEnd => _flags == (HitEndFlag | NotFinalFlag);

    /// <summary>
    /// Records that the current decision depended on text beyond the end of <see cref="Buffer"/>.
    /// Custom parsers that inspect <see cref="Span"/> or <see cref="Buffer"/> directly must call this when they stop,
    /// or fail, because the buffer ended rather than because of its content.
    /// Moving the cursor to the end, <see cref="PeekNext(int)"/> past it and the <c>Match</c> methods already do it.
    /// </summary>
    public void MarkHitEnd()
    {
        _flags |= HitEndFlag;
    }

    internal void ResetHitEnd()
    {
        _flags = (byte)((_flags & (NotFinalFlag | FollowedByLineFeedFlag)) | (Eof ? HitEndFlag : 0));
    }

    /// <summary>
    /// Records that a <c>'\n'</c> follows the end of a final <see cref="Buffer"/> in the input, as the delimiter of a frame
    /// of a stream: a <c>'\r'</c> ending the buffer doesn't end a line when <see cref="NewLines"/> has both.
    /// </summary>
    internal void MarkFollowedByLineFeed()
    {
        if (IsFinal)
        {
            _flags |= FollowedByLineFeedFlag;
        }
    }

    public Cursor(string buffer) : this(buffer, TextPosition.Start)
    {
    }

    /// <summary>
    /// The chars which end a line, <c>"\n"</c> by default. When both <c>'\r'</c> and <c>'\n'</c> are in the set, <c>"\r\n"</c>
    /// ends a single line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The lines and columns of <see cref="Position"/>, <see cref="GetPosition(int)"/> and <see cref="FurthestPosition"/>, and
    /// thus of the errors of a parse, are counted with them. JavaScript, for instance, ends lines with
    /// <c>"\n\r\u2028\u2029"</c>, and C# also with <c>'\u0085'</c>. They don't change what the parsers read.
    /// </para>
    /// <para>
    /// Set them before positions are requested: setting them forgets the lines counted so far. When parsing a
    /// <see cref="System.IO.TextReader"/>, set them in <c>StreamParseOptions.ContextFactory</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The value is empty.</exception>
    public string NewLines
    {
        get => _newLines ?? DefaultNewLines;
        set
        {
            ThrowHelper.ThrowIfNull(value, nameof(value));

            if (value.Length == 0)
            {
                throw new ArgumentException("At least one char must end a line.", nameof(value));
            }

            _newLines = value == DefaultNewLines ? null : value;
            _lines = null;
        }
    }

    private const string DefaultNewLines = "\n";

    /// <summary>
    /// The current position. The offset is absolute, the line starts at 1 and increases after each line terminator of
    /// <see cref="NewLines"/>, <c>'\n'</c> by default, and the column is one more than the number of chars between the end of
    /// the previous line (or the start of the text) and the position: every char which doesn't end a line, <c>'\r'</c>
    /// included by default, occupies one column.
    /// </summary>
    /// <remarks>
    /// The line and the column are computed when this property is read, from the text, rather than tracked as the cursor
    /// moves, see <see cref="GetPosition(int)"/>. Parsers which save a position to move the cursor back to it should save
    /// <see cref="Offset"/> and call <see cref="ResetPosition(int)"/> instead.
    /// </remarks>
    public TextPosition Position => GetPosition(_offset + _base);

    /// <summary>
    /// Returns the position of an absolute offset of the buffered text, with its line and column, see <see cref="Position"/>.
    /// </summary>
    /// <param name="offset">An absolute offset, like <see cref="Offset"/> or the offsets a <c>Then</c> callback receives.</param>
    /// <remarks>
    /// <para>
    /// The offsets can be requested in any order. The cursor records the start of each line it counts over, the first time a
    /// position after it is requested, such that the text is read at most once and the position of an offset already
    /// counted over is a binary search. A parse which doesn't request positions doesn't count lines.
    /// </para>
    /// <para>
    /// An offset between the <c>'\r'</c> and the <c>'\n'</c> of a <c>"\r\n"</c> is on the line they end. The lines and columns
    /// of the offsets before the initial position of the cursor are counted backward from it, the start of the buffer
    /// starting a line, each time they are requested. In compacting mode, the text discarded from the buffer can't be
    /// counted: an offset in it gets the line of the first buffered char, and its column assuming that no line ends between
    /// them. A <c>'\r'</c> which ends a buffer of a stream that more text follows is counted as the end of a line when the
    /// position after it is requested before the next char is read. An offset after the end of the buffer gets the line of
    /// its end, each char after it occupying a column.
    /// </para>
    /// </remarks>
    public TextPosition GetPosition(int offset)
    {
        if (offset < _originOffset || offset - _base > _textLength)
        {
            return GetPositionOutsideOrigin(offset);
        }

        var lines = _lines ??= new LineMap(_newLines ?? DefaultNewLines, _originOffset);

        if (offset > lines.Counted && !CountLines(lines, offset))
        {
            // The offset is the end of a buffer ending with '\r', which the next buffer tells whether a '\n' follows
            var carriageReturn = FindPosition(lines, lines.Counted);
            return new TextPosition(offset, carriageReturn.Line + 1, 1);
        }

        return FindPosition(lines, offset);
    }

    // The position of an offset between the origin and the furthest offset counted
    private TextPosition FindPosition(LineMap lines, int offset)
    {
        var starts = lines.Starts;
        var count = lines.Count;
        int index;

        // The requests are usually at the frontier of the text counted, or just behind it
        if (count == 0 || offset >= starts[count - 1])
        {
            index = count - 1;
        }
        else
        {
            index = Array.BinarySearch(starts, 0, count, offset);

            if (index < 0)
            {
                // The line starting before the offset
                index = ~index - 1;
            }
        }

        return index < 0
            ? new TextPosition(offset, _originLine, _originColumn + (offset - _originOffset))
            : new TextPosition(offset, _originLine + index + 1, offset - starts[index] + 1);
    }

    // Records the starts of the lines up to an offset of the buffer, from the furthest offset counted. Returns false when
    // the offset is the end of a buffer which more text follows and ends with a '\r' that a '\n' can follow: the '\r'
    // isn't counted then, and the lines are counted up to it.
    private bool CountLines(LineMap lines, int offset)
    {
        var from = lines.Counted;
        var text = GetSpan(from, offset - from);
        var newLines = lines.NewLines.AsSpan();
        var position = 0;
        int index;

        while ((index = text.Slice(position).IndexOfAny(newLines)) >= 0)
        {
            var end = position + index;
            position = end + 1;

            if (lines.CrLf && text[end] == '\r')
            {
                var follows = FollowsCarriageReturn(from + end, -1);

                if (follows == FollowedByLineFeed)
                {
                    // The line ends after the '\n'
                    continue;
                }

                if (follows == FollowedByUnknown)
                {
                    lines.Counted = from + end;
                    return false;
                }
            }

            lines.Add(from + end + 1);
        }

        lines.Counted = offset;
        return true;
    }

    // The position of the offset the start of a new window moves the origin to, see ReplaceBuffer. It doesn't record the
    // lines it counts, which are dropped with the text. The char after the end of the buffer is next, or -1.
    private TextPosition GetPositionToDrop(int offset, int next)
    {
        var lines = _lines;

        if (lines is not null && offset <= lines.Counted)
        {
            return FindPosition(lines, offset);
        }

        var from = lines is null ? new TextPosition(_originOffset, _originLine, _originColumn) : FindPosition(lines, lines.Counted);
        var count = CountLineStarts(from.Offset, offset, next, out var lastStart);

        return count == 0
            ? new TextPosition(offset, from.Line, from.Column + (offset - from.Offset))
            : new TextPosition(offset, from.Line + count, offset - lastStart + 1);
    }

    // The offsets before the origin, or after the end of the buffer, are not recorded such that the lines recorded are
    // always counted from the origin
    private TextPosition GetPositionOutsideOrigin(int offset)
    {
        var end = _base + _textLength;

        if (offset > end)
        {
            // Only after ResetPosition moved the cursor past the end of the text: the extra chars are counted as columns
            var last = GetPosition(end);
            return new TextPosition(offset, last.Line, last.Column + (offset - end));
        }

        if (offset < _base)
        {
            // The text was discarded by a compacting stream, the origin is at the start of the buffer
            return new TextPosition(offset, _originLine, Math.Max(1, _originColumn - (_originOffset - offset)));
        }

        var newLines = CountLineStarts(offset, _originOffset, -1, out _);

        return newLines == 0
            ? new TextPosition(offset, _originLine, Math.Max(1, _originColumn - (_originOffset - offset)))
            : new TextPosition(offset, _originLine - newLines, offset - FindLineStart(offset) + 1);
    }

    // The number of lines which start after the offset from and up to the offset to of the buffer, and the start of the
    // last one. The char after the end of the buffer is next, or -1 when it isn't known.
    private int CountLineStarts(int from, int to, int next, out int lastStart)
    {
        var newLines = NewLines.AsSpan();
        var crLf = IsCrLf(newLines);
        var text = GetSpan(from, to - from);
        var count = 0;

        lastStart = -1;

#if NET8_0_OR_GREATER
        if (newLines.Length == 1)
        {
            // The common case, vectorized
            count = text.Count(newLines[0]);

            if (count > 0)
            {
                lastStart = from + text.LastIndexOf(newLines[0]) + 1;
            }

            return count;
        }
#endif

        var position = 0;
        int index;

        while ((index = text.Slice(position).IndexOfAny(newLines)) >= 0)
        {
            var end = position + index;
            position = end + 1;

            if (crLf && text[end] == '\r' && FollowsCarriageReturn(from + end, next) == FollowedByLineFeed)
            {
                continue;
            }

            count++;
            lastStart = from + end + 1;
        }

        return count;
    }

    // The start of the line of an offset of the buffer, the start of the buffer starting a line
    private int FindLineStart(int offset)
    {
        var newLines = NewLines.AsSpan();
        var crLf = IsCrLf(newLines);
        var text = GetSpan(_base, offset - _base);

        while (true)
        {
            var end = text.LastIndexOfAny(newLines);

            if (end < 0)
            {
                return _base;
            }

            if (!crLf || text[end] != '\r' || FollowsCarriageReturn(_base + end, -1) != FollowedByLineFeed)
            {
                return _base + end + 1;
            }

            text = text.Slice(0, end);
        }
    }

    // What follows a '\r' at an offset of the buffer, when "\r\n" ends a single line: a '\n', another char or the end of
    // the input, which end the line at the '\r', or the end of a buffer which more text follows. The char after the end of
    // the buffer is next, or -1 when it isn't known.
    private int FollowsCarriageReturn(int offset, int next)
    {
        var index = offset + 1 - _base;

        if (index < _textLength)
        {
            return Buffer[index] == '\n' ? FollowedByLineFeed : FollowedByOther;
        }

        if (next == '\n' || (_flags & FollowedByLineFeedFlag) != 0)
        {
            return FollowedByLineFeed;
        }

        return next >= 0 || IsFinal ? FollowedByOther : FollowedByUnknown;
    }

    private const int FollowedByOther = 0;
    private const int FollowedByLineFeed = 1;
    private const int FollowedByUnknown = 2;

    // Whether "\r\n" ends a single line
    private static bool IsCrLf(ReadOnlySpan<char> newLines) => newLines.IndexOf('\r') >= 0 && newLines.IndexOf('\n') >= 0;

    /// <summary>
    /// The position of the end of the buffer when the input goes on with <paramref name="next"/>, or <c>-1</c> at its end:
    /// a <c>'\r'</c> ending the buffer doesn't end a line when a <c>'\n'</c> follows it.
    /// </summary>
    internal TextPosition GetEndPosition(int next)
    {
        var end = _base + _textLength;

        if (next == '\n' && _textLength > 0 && Buffer[_textLength - 1] == '\r' && IsCrLf(NewLines.AsSpan()))
        {
            var carriageReturn = GetPosition(end - 1);
            return new TextPosition(end, carriageReturn.Line, carriageReturn.Column + 1);
        }

        return GetPosition(end);
    }

    /// <summary>
    /// Whether a char which follows another one ends a line, <paramref name="next"/> being the char after it or <c>-1</c>
    /// at the end of the input.
    /// </summary>
    internal bool EndsLine(char c, int next)
    {
        var newLines = NewLines.AsSpan();
        return newLines.IndexOf(c) >= 0 && !(c == '\r' && next == '\n' && IsCrLf(newLines));
    }

    // The line terminators, and the starts of the lines after the origin which were counted
    private sealed class LineMap
    {
        public LineMap(string newLines, int counted)
        {
            NewLines = newLines;
            CrLf = IsCrLf(newLines.AsSpan());
            Counted = counted;
        }

        public readonly string NewLines;

        // Whether "\r\n" ends a single line
        public readonly bool CrLf;

        // The absolute offsets of the starts of the lines after the origin's, in order. Line _originLine + 1 + i starts at Starts[i].
        public int[] Starts = Array.Empty<int>();
        public int Count;

        // The absolute offset up to which the starts of the lines are recorded
        public int Counted;

        public void Add(int start)
        {
            if (Count == Starts.Length)
            {
                Array.Resize(ref Starts, Math.Max(16, Count * 2));
            }

            Starts[Count++] = start;
        }

        // Forgets the lines which start before an offset the origin moves to, see ReplaceBuffer
        public void DropUpTo(int offset)
        {
            var dropped = Array.BinarySearch(Starts, 0, Count, offset);

            // The number of lines starting before the offset, or at it
            dropped = dropped >= 0 ? dropped + 1 : ~dropped;

            Count -= dropped;
            Array.Copy(Starts, dropped, Starts, 0, Count);

            if (Counted < offset)
            {
                Counted = offset;
            }
        }
    }

    /// <summary>
    /// Returns the <see cref="ReadOnlySpan{T}"/> value of the <see cref="Buffer" /> at the current offset.
    /// </summary>
    public ReadOnlySpan<char> Span => Buffer.AsSpan(_offset);

    // The absolute offset of the first char of Buffer, 0 unless the input is streamed in compacting mode
    internal int BufferStart => _base;

    /// <summary>
    /// Returns the buffered text between the absolute offset <paramref name="start"/> and <c>start + length</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<char> GetSpan(int start, int length) => Buffer.AsSpan(start - _base, length);

    /// <summary>
    /// Creates a <see cref="TextSpan"/> over the buffered text between the absolute offset <paramref name="start"/> and <c>start + length</c>.
    /// </summary>
    /// <remarks>
    /// The <see cref="TextSpan.Offset"/> of the result is relative to its <see cref="TextSpan.Buffer"/>, which is the
    /// current <see cref="Buffer"/>. Parsers must create their <see cref="TextSpan"/> results with this method rather than
    /// from <see cref="Buffer"/> and an absolute offset, which are only equivalent when the input isn't streamed in compacting mode.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextSpan CreateSpan(int start, int length) => new(Buffer, start - _base, length);

    /// <summary>
    /// Whether the current position was discarded from the buffer, which only happens after a parser moved the cursor
    /// back to text it didn't retain in compacting mode.
    /// </summary>
    internal bool IsDiscarded => _offset < 0;

    /// <summary>
    /// The number of chars buffered after the current position.
    /// </summary>
    internal int Remaining => _textLength - _offset;

#if !PARLOT_STRING_ONLY
    /// <summary>
    /// Replaces <see cref="Buffer"/> with a window of the same input, keeping the absolute position.
    /// </summary>
    /// <param name="buffer">The new window.</param>
    /// <param name="bufferStart">The absolute offset of <paramref name="buffer"/>'s first char. It must not be past the current position.</param>
    /// <param name="isFinal">Whether <paramref name="buffer"/> ends with the end of the input.</param>
    internal void ReplaceBuffer(string buffer, int bufferStart, bool isFinal)
    {
        var offset = _offset + _base;

        // The lines are counted from the origin, the text before the new window is dropped: its lines are counted first
        if (bufferStart > _originOffset)
        {
            var end = _base + _textLength;

            // The new window tells whether a '\r' ending the current one is followed by a '\n'
            var next = end - bufferStart < buffer.Length ? buffer[end - bufferStart] : -1;
            var start = GetPositionToDrop(bufferStart, next);

            _originOffset = bufferStart;
            _originLine = start.Line;
            _originColumn = start.Column;
            _lines?.DropUpTo(bufferStart);
        }

        Buffer = buffer;
        _textLength = buffer.Length;
        _base = bufferStart;
        _offset = offset - bufferStart;
        _flags = (byte)((_flags & HitEndFlag) | (isFinal ? 0 : NotFinalFlag));

        if (_offset >= _textLength)
        {
            _offset = _textLength;
            Eof = true;
            Current = NullChar;
        }
        else
        {
            Eof = false;
            Current = Buffer[_offset];
        }
    }
#endif

    /// <summary>
    /// Advances the cursor by one character. Nothing happens when the cursor is already at the end of the text.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance()
    {
        var offset = _offset + 1;

        if (offset < _textLength)
        {
            _offset = offset;
            Current = Buffer[offset];
        }
        else
        {
            AdvanceToEnd();
        }
    }

    /// <summary>
    /// Advances the cursor by <paramref name="count"/> characters, or to the end of the text.
    /// Nothing happens when the cursor is already at the end of the text, or when <paramref name="count"/> is negative.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Advance(int count)
    {
        if (Eof)
        {
            return;
        }

        // A single unsigned compare selects a count which doesn't reach the end of the text, and isn't negative
        if (unchecked((uint)count < (uint)(_textLength - _offset)))
        {
            _offset += count;
            Current = Buffer[_offset];
        }
        else if (count > 0)
        {
            AdvanceToEnd();
        }
    }

    /// <summary>
    /// Advances the cursor by <paramref name="offset"/> characters, or to the end of the text.
    /// </summary>
    /// <remarks>
    /// Lines and columns are computed from the text when <see cref="Position"/> is read, so the chars the cursor leaves can
    /// contain <c>'\n'</c> too: this is <see cref="Advance(int)"/>, which it predates, except that it doesn't check that
    /// <paramref name="offset"/> isn't negative.
    /// </remarks>
    public void AdvanceNoNewLines(int offset)
    {
        var newOffset = _offset + offset;

        if (newOffset >= _textLength)
        {
            AdvanceToEnd();
            return;
        }

        Current = Buffer[newOffset];
        _offset = newOffset;
    }

    /// <summary>
    /// Advances the cursor by <paramref name="count"/> chars which are in the buffer, up to its end included.
    /// </summary>
    /// <remarks>
    /// This is <see cref="AdvanceNoNewLines(int)"/> inlined: generated parsers which read the text through a span call it
    /// once the text matched, with the number of chars they read.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AdvanceWithinBuffer(int count)
    {
        var offset = _offset + count;
        var buffer = Buffer;

        // The length of the buffer, rather than _textLength which is the same, lets the JIT remove the bounds check
        if ((uint)offset < (uint)buffer.Length)
        {
            _offset = offset;
            Current = buffer[offset];
        }
        else
        {
            AdvanceToEnd();
        }
    }

    // Moving to the end of the text, or staying there, which records that the end of the buffer was reached
    private void AdvanceToEnd()
    {
        _offset = _textLength;
        Eof = true;
        _flags |= HitEndFlag;
        Current = NullChar;
    }

    /// <summary>
    /// Moves the cursor to an absolute offset, usually a value of <see cref="Offset"/> read before the cursor advanced.
    /// </summary>
    /// <param name="offset">The absolute offset to move to.</param>
    /// <remarks>
    /// This is the way for a parser to move the cursor back when it doesn't match: saving <see cref="Offset"/> costs nothing
    /// whereas reading <see cref="Position"/> computes the line and the column.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetPosition(int offset)
    {
        if (offset - _base != _offset)
        {
            ResetPositionNotInlined(offset);
        }
    }

    /// <summary>
    /// Moves the cursor to the offset of a position.
    /// </summary>
    /// <param name="position">The position to move to.</param>
    /// <remarks>
    /// Only <see cref="TextPosition.Offset"/> is used: the line and the column of the cursor are always computed from the
    /// text, so a position whose line or column don't match its offset doesn't change them. Prefer saving <see cref="Offset"/>
    /// and calling <see cref="ResetPosition(int)"/>, which doesn't compute the line and the column.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetPosition(in TextPosition position) => ResetPosition(position.Offset);

    private void ResetPositionNotInlined(int offset)
    {
        // A parser moves the cursor back when it fails after reading: the position it read up to is a candidate for the
        // furthest failure. Only this path, which runs when the cursor actually moves, pays the comparison.
        var from = _offset + _base;
        var furthest = _furthestOffset;
        _furthestOffset = from > furthest ? from : furthest;

        _offset = offset - _base;

        // A single unsigned compare covers both the end of the buffer and, in compacting mode, a discarded position.
        // Unchecked since the offset is negative for a discarded position, and generated parsers embed this file in
        // assemblies which can check for arithmetic overflows.
        if (unchecked((uint)_offset) < (uint)Buffer.Length)
        {
            Current = Buffer[_offset];
            Eof = false;
        }
        else
        {
            ResetPositionOutsideBuffer();
        }
    }

    /// <summary>
    /// Moves the cursor to an absolute offset which was read up to already: forward over text that matched (white space
    /// which was skipped before, what a parser read without moving the cursor, a match that is kept), or back to where
    /// such a parser reads on from, after what it looked for further wasn't there.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="ResetPosition(int)"/>, the offset the cursor leaves is a candidate for the furthest failure only
    /// when the cursor moves back. What was read up to an offset it moves forward from matched, and a value that is
    /// rejected after it would be reported there instead of at its start.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void MoveTo(int offset)
    {
        if (offset - _base != _offset)
        {
            MoveToNotInlined(offset);
        }
    }

    // ResetPositionNotInlined, recording only the moves back. It isn't shared with it: the parsers which reset the cursor
    // the most inline that method, and the code of the expression benchmarks changed with its size.
    private void MoveToNotInlined(int offset)
    {
        var from = _offset + _base;

        if (from > offset)
        {
            RecordFailure(from);
        }

        _offset = offset - _base;

        if (unchecked((uint)_offset) < (uint)Buffer.Length)
        {
            Current = Buffer[_offset];
            Eof = false;
        }
        else
        {
            ResetPositionOutsideBuffer();
        }
    }

    private void ResetPositionOutsideBuffer()
    {
        Current = NullChar;

        if (_offset < 0)
        {
            // The text was discarded by a compacting stream: nothing can be read until the cursor moves forward again.
            Eof = false;
        }
        else
        {
            // Eof might have been recorded
            Eof = true;
            _flags |= HitEndFlag;
        }
    }

    /// <summary>
    /// The furthest absolute offset the parse reached: the furthest offset the cursor was moved back from, or recorded by a
    /// parser that read further without moving the cursor, or the current offset when it is further.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Parsers move the cursor back when they fail after reading, so when a parse fails this is the classic "furthest
    /// failure" position: where the longest attempt to match the input stopped. It is meaningful after a failed parse, a
    /// successful one reports at least where it ended.
    /// </para>
    /// <para>
    /// The <c>Parse</c>, <c>TryParse</c> and <c>TryMatch</c> methods taking a <c>ParseContext</c> reset it with
    /// <see cref="ResetFurthestOffset"/> before they parse with a context, so that a context which is reused reports the
    /// failure of its last parse. A failure through <c>Error</c> or <c>ElseError</c> is reported by the exception's position.
    /// </para>
    /// </remarks>
    public int FurthestOffset
    {
        get
        {
            var offset = _offset + _base;
            return offset > _furthestOffset ? offset : _furthestOffset;
        }
    }

    /// <summary>
    /// The position of <see cref="FurthestOffset"/>, with its line and column.
    /// </summary>
    /// <remarks>
    /// The line and the column are computed from the text when it is read, like <see cref="Position"/>.
    /// </remarks>
    public TextPosition FurthestPosition => GetPosition(FurthestOffset);

    /// <summary>
    /// Forgets the failures recorded so far: <see cref="FurthestOffset"/> becomes the current offset.
    /// </summary>
    /// <remarks>
    /// Call it before a parse with a context that was already used, when not parsing through the methods which do it,
    /// such as <c>TryParse(ParseContext, ...)</c>.
    /// </remarks>
    public void ResetFurthestOffset()
    {
        _furthestOffset = _offset + _base;
    }

    /// <summary>
    /// The furthest offset recorded so far, for <see cref="RestoreRecordedFailure(int)"/>.
    /// </summary>
    internal int RecordedFailure => _furthestOffset;

    /// <summary>
    /// Forgets the failures recorded since <paramref name="recordedFailure"/> was read from <see cref="RecordedFailure"/>.
    /// </summary>
    /// <remarks>
    /// For what reading past a position doesn't tell about the input at that position: a lookahead that matched, or which
    /// isn't there to match, and a value which a condition rejected once it was read.
    /// </remarks>
    internal void RestoreRecordedFailure(int recordedFailure)
    {
        _furthestOffset = recordedFailure;
    }

    /// <summary>
    /// Records an absolute offset a parser read up to without moving the cursor there, before it failed.
    /// </summary>
    /// <remarks>Generated span code reads the text through a local position and calls it on its failure path.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RecordFailure(int offset)
    {
        if (offset > _furthestOffset)
        {
            _furthestOffset = offset;
        }
    }

    /// <summary>
    /// Evaluates the char at the current position.
    /// </summary>
    public char Current { get; private set; }

    /// <summary>
    /// Returns the cursor's absolute offset in the input.
    /// </summary>
    public int Offset
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _offset + _base;
    }

    /// <summary>
    /// Evaluates a char forward in the _buffer.
    /// </summary>
    public char PeekNext(int index = 1)
    {
        var nextIndex = _offset + index;

        if (nextIndex >= _textLength || nextIndex < 0)
        {
            if (nextIndex >= _textLength)
            {
                _flags |= HitEndFlag;
            }

            return NullChar;
        }

        return Buffer[nextIndex];
    }

    /// <summary>
    /// Advances the cursor by <paramref name="consumedLength"/> characters, or to the end of the text.
    /// </summary>
    /// <param name="consumedLength">How many characters were consumed.</param>
    /// <param name="newLines">Ignored: lines and columns are computed from the text when <see cref="Position"/> is read.</param>
    /// <param name="trailingSegmentLength">Ignored: lines and columns are computed from the text when <see cref="Position"/> is read.</param>
    /// <remarks>Prefer <see cref="Advance(int)"/>, the line and column arguments are only kept for compatibility.</remarks>
    public void AdvanceBy(int consumedLength, int newLines, int trailingSegmentLength)
    {
        _offset += consumedLength;

        if (_offset >= Buffer.Length)
        {
            Eof = true;
            _flags |= HitEndFlag;
            _offset = Buffer.Length;
            Current = NullChar;
        }
        else
        {
            Eof = false;
            Current = Buffer[_offset];
        }
    }

    public bool Eof { get; private set; }

    /// <summary>
    /// The buffered text. When the input is streamed in compacting mode it only holds the text the parser can still read,
    /// so read it with <see cref="Span"/>, <see cref="GetSpan(int, int)"/> or <see cref="CreateSpan(int, int)"/>, which take absolute offsets,
    /// rather than by indexing it with an offset.
    /// </summary>
    public string Buffer { get; private set; }

    /// <summary>
    /// Whether a char is at the current position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Match(char c)
    {
        // Ordinal comparison. Current is also the null char at the end of the text, where there is no char to match.
        return Current == c && (c != NullChar || !Eof);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool MatchAnyOf(ReadOnlySpan<char> s)
    {
        if (Eof)
        {
            return false;
        }

        return s.Length == 0 || s.IndexOf(Current) > -1;
    }

    /// <summary>
    /// Whether a string is at the current position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Match(ReadOnlySpan<char> s)
    {
        // Equivalent to StringComparison.Ordinal comparison

        if (_textLength < _offset + s.Length)
        {
            MarkHitEndIfPrefix(s, StringComparison.Ordinal);
            return false;
        }

        return Span.StartsWith(s);
    }

    /// <summary>
    /// Whether a string is at the current position.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Match(ReadOnlySpan<char> s, StringComparison comparisonType)
    {
        if (_textLength < _offset + s.Length)
        {
            MarkHitEndIfPrefix(s, comparisonType);
            return false;
        }

        return Span.StartsWith(s, comparisonType);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void MarkHitEndIfPrefix(ReadOnlySpan<char> s, StringComparison comparisonType)
    {
        if (IsFinal)
        {
            return;
        }

        // A shorter remainder only proves a mismatch when it already differs from the pattern.
        // Culture-sensitive comparisons are not prefix-stable, so they always depend on more text.
        if ((comparisonType != StringComparison.Ordinal && comparisonType != StringComparison.OrdinalIgnoreCase)
            || s.StartsWith(Span, comparisonType))
        {
            _flags |= HitEndFlag;
        }
    }
}
