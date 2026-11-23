using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Parlot.Fluent;

/// <summary>
/// Reads a <see cref="TextReader"/> into a window of text which the synchronous parsers run on.
/// </summary>
/// <remarks>
/// A parse that reads past the end of the window, see <see cref="Cursor.HitEnd"/>, is inconclusive. The text before the
/// position it started from is dropped, more text is read and the parse is retried on a new window. Each window is an
/// immutable string so the <see cref="TextSpan"/> values of previous results remain valid. Offsets are relative to the
/// current window while lines and columns are absolute.
/// </remarks>
internal sealed class StreamingDriver : IDisposable
{
    private readonly TextReader _reader;
    private readonly StreamParseOptions _options;
    private readonly CancellationToken _cancellationToken;

    // Text read but not dropped yet, it starts at index 0
    private char[] _chars;
    private int _length;

    // Absolute position of the window start
    private long _offset;

    // The char before the window start, -1 at the start of the input, see Cursor.IsAtLineStart
    private int _charBefore = -1;
    private int _line = 1;
    private int _column = 1;

    // Current frame when the text is split by a delimiter, see NextFrameAsync
    private int _frameStart;
    private int _frameLength = -1;
    private bool _frameDelimited;
    private bool _pendingDelimiter;

    public StreamingDriver(TextReader reader, StreamParseOptions? options, CancellationToken cancellationToken)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _options = options ?? StreamParseOptions.Default;
        _cancellationToken = cancellationToken;

        _options.Validate();

        _chars = [];
    }

    public StreamParseOptions Options => _options;

    /// <summary>
    /// The context for the current window.
    /// </summary>
    public ParseContext? Context { get; private set; }

    public Cursor Cursor => Context!.Scanner.Cursor;

    /// <summary>
    /// Whether the window holds the end of the input.
    /// </summary>
    public bool IsFinal { get; private set; }

    /// <summary>
    /// Drops the text before <paramref name="start"/>, reads more text and starts a new window at <paramref name="start"/>.
    /// </summary>
    /// <param name="start">The offset of the text to keep in the current window.</param>
    public async ValueTask GrowAsync(int start)
    {
        // The cursor of the current window, which tells the line and column of the start of the new one once it is read
        var cursor = Context?.Scanner.Cursor;
        var atEnd = start == _length;
        var available = _length - start;

        if (available >= _options.MaxBufferedCharacters)
        {
            var errorPosition = cursor is not null ? cursor.GetPosition(start) : new TextPosition(start, _line, _column);
            throw new ParseException($"The input requires more than {_options.MaxBufferedCharacters} buffered characters to be parsed.", ToAbsolute(errorPosition));
        }

        // At the end of the current window, a '\r' ending it is counted as the end of a line until the next char is read
        if (cursor is not null)
        {
            var position = cursor.GetPosition(start);
            _line = position.Line;
            _column = position.Column;
        }

        if (start > 0)
        {
            _charBefore = _chars[start - 1];
            Array.Copy(_chars, start, _chars, 0, available);
            _length = available;
            _offset += start;
        }

        // Grow geometrically so that the retries cost a bounded multiple of a single parse
        var target = available + Math.Max(available, _options.BufferSize);

        if (target < 0 || target > _options.MaxBufferedCharacters)
        {
            target = _options.MaxBufferedCharacters;
        }

        if (_chars.Length < target)
        {
            var chars = ArrayPool<char>.Shared.Rent(target);
            Array.Copy(_chars, chars, _length);
            ReturnChars();
            _chars = chars;
        }

        // Never retain more than the limit
        var capacity = Math.Min(_chars.Length, _options.MaxBufferedCharacters);

        while (_length < target)
        {
            _cancellationToken.ThrowIfCancellationRequested();

#if NET8_0_OR_GREATER
            var read = await _reader.ReadAsync(_chars.AsMemory(_length, capacity - _length), _cancellationToken).ConfigureAwait(false);
#else
            var read = await _reader.ReadAsync(_chars, _length, capacity - _length).ConfigureAwait(false);
#endif

            if (read == 0)
            {
                IsFinal = true;
                break;
            }

            _length += read;
        }

        // It doesn't when a '\n' starts the new window
        if (cursor is not null && atEnd && _length > 0 && _chars[0] == '\n')
        {
            cursor.MarkFollowedByLineFeed();
            var position = cursor.GetPosition(start);
            _line = position.Line;
            _column = position.Column;
        }

        Context = CreateContext(new string(_chars, 0, _length), IsFinal);
    }

    /// <summary>
    /// Starts a window on the next non-empty text ending before <paramref name="delimiter"/> or at the end of the input.
    /// The window is final. The cursor of the previous window must be at its end.
    /// </summary>
    /// <returns><see langword="false"/> when there is no more text.</returns>
    public async ValueTask<bool> NextFrameAsync(char delimiter)
    {
        while (true)
        {
            if (_frameLength >= 0)
            {
                // The previous frame is consumed, its end is the position of the delimiter
                var end = Cursor.Position;
                _line = end.Line;
                _column = end.Column;

                var consumed = _frameLength + (_frameDelimited ? 1 : 0);

                // The next frame follows the delimiter
                if (_frameDelimited)
                {
                    _charBefore = delimiter;
                }
                _offset += consumed;
                _frameStart += consumed;
                _pendingDelimiter = _frameDelimited;
                _frameLength = -1;
            }

            var scanned = _frameStart;

            while (true)
            {
                var index = _chars.AsSpan(scanned, _length - scanned).IndexOf(delimiter);

                if (index >= 0)
                {
                    _frameLength = scanned + index - _frameStart;
                    _frameDelimited = true;
                    break;
                }

                if (IsFinal)
                {
                    if (_length == _frameStart)
                    {
                        return false;
                    }

                    _frameLength = _length - _frameStart;
                    _frameDelimited = false;
                    break;
                }

                scanned = _length - _frameStart;
                await ReadFrameAsync(delimiter).ConfigureAwait(false);
            }

            ApplyPendingDelimiter(delimiter);

            if (_frameLength > 0)
            {
                Context = CreateContext(new string(_chars, _frameStart, _frameLength), isFinal: true);

                // A '\r' ending the frame doesn't end a line when the delimiter is a '\n', see Cursor.NewLines
                if (_frameDelimited && delimiter == '\n')
                {
                    Cursor.MarkFollowedByLineFeed();
                }

                return true;
            }

            // Skip empty frames without creating a throwaway Context
            if (_frameDelimited)
            {
                _charBefore = delimiter;
                _offset++;
                _frameStart++;
                _pendingDelimiter = true;
            }

            _frameLength = -1;
        }
    }

    /// <summary>
    /// Moves the text of the current frame to the start of the buffer and reads more text once.
    /// </summary>
    private async ValueTask ReadFrameAsync(char delimiter)
    {
        var available = _length - _frameStart;

        if (available >= _options.MaxBufferedCharacters)
        {
            ApplyPendingDelimiter(delimiter);
            throw new ParseException($"The input requires more than {_options.MaxBufferedCharacters} buffered characters to be parsed.", ToAbsolute(new TextPosition(0, _line, _column)));
        }

        if (_frameStart > 0)
        {
            Array.Copy(_chars, _frameStart, _chars, 0, available);
            _length = available;
            _frameStart = 0;
        }

        if (_length == _chars.Length)
        {
            var size = Math.Max(_options.BufferSize, _length * 2);

            if (size < 0 || size > _options.MaxBufferedCharacters)
            {
                size = _options.MaxBufferedCharacters;
            }

            var chars = ArrayPool<char>.Shared.Rent(size);
            Array.Copy(_chars, chars, _length);
            ReturnChars();
            _chars = chars;
        }

        _cancellationToken.ThrowIfCancellationRequested();

        var capacity = Math.Min(_chars.Length, _options.MaxBufferedCharacters);

#if NET8_0_OR_GREATER
        var read = await _reader.ReadAsync(_chars.AsMemory(_length, capacity - _length), _cancellationToken).ConfigureAwait(false);
#else
        var read = await _reader.ReadAsync(_chars, _length, capacity - _length).ConfigureAwait(false);
#endif

        if (read == 0)
        {
            IsFinal = true;
        }
        else
        {
            _length += read;
        }
    }

    /// <summary>
    /// Moves the position past a delimiter once the character which follows it is known.
    /// </summary>
    private void ApplyPendingDelimiter(char delimiter)
    {
        if (!_pendingDelimiter)
        {
            return;
        }

        _pendingDelimiter = false;

        // Same rule as the cursor, whose line terminators are the ones of the frame before it.
        // Without a ContextFactory, the cursor uses the default "\n" terminator, avoiding a context allocation before the first non-empty frame.
        var next = _length > _frameStart ? _chars[_frameStart] : -1;
        var endsLine = Context is not null
            ? Context.Scanner.Cursor.EndsLine(delimiter, next)
            : _options.ContextFactory is null
                ? delimiter == '\n'
                : (Context = CreateContext("", isFinal: false)).Scanner.Cursor.EndsLine(delimiter, next);

        if (endsLine)
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }
    }

    /// <summary>
    /// Converts a position in the current window to a position in the input, saturating offsets beyond <see cref="int.MaxValue"/>.
    /// </summary>
    public TextPosition ToAbsolute(in TextPosition position)
    {
        var offset = _offset + position.Offset;
        return new TextPosition(offset > int.MaxValue ? int.MaxValue : (int)offset, position.Line, position.Column);
    }

    public ParseException ToAbsolute(ParseException exception)
    {
        // An exception created without a position, like a callback's, has line 0, which no position in a text has
        if (exception.Position.Line != 0)
        {
            exception.Position = ToAbsolute(exception.Position);
        }

        return exception;
    }

    private ParseContext CreateContext(string text, bool isFinal)
    {
        var scanner = new Scanner(text, new TextPosition(0, _line, _column), isFinal);
        var context = _options.CreateContext(scanner, _cancellationToken);

        scanner.Cursor.SetCharBeforeBuffer(_charBefore);

        return context;
    }

    private void ReturnChars()
    {
        if (_chars.Length > 0)
        {
            ArrayPool<char>.Shared.Return(_chars);
        }
    }

    public void Dispose()
    {
        ReturnChars();
        _chars = [];
        _length = 0;
    }
}

/// <summary>
/// Parses successive items, optionally separated, from a <see cref="StreamingDriver"/>.
/// </summary>
internal sealed class StreamingItemReader<T, TSeparator>
{
    private readonly StreamingDriver _driver;
    private readonly Parser<T> _parser;
    private readonly Parser<TSeparator>? _separator;
    private readonly bool _skipWhiteSpace;

    private bool _expectSeparator;
    private bool _expectEnd;

    public StreamingItemReader(StreamingDriver driver, Parser<T> parser, Parser<TSeparator>? separator)
    {
        _driver = driver;
        _parser = parser;
        _separator = separator;
        _skipWhiteSpace = driver.Options.SkipWhiteSpace;
    }

    /// <summary>
    /// The offset to retry from when <see cref="Next(out T)"/> returns <see cref="StreamingStep.NeedMore"/>.
    /// </summary>
    public int RetryOffset { get; private set; }

    public StreamingStep Next(out T value)
    {
        value = default!;

        while (true)
        {
            var context = _driver.Context;

            if (context is null)
            {
                RetryOffset = 0;
                return StreamingStep.NeedMore;
            }

            var cursor = context.Scanner.Cursor;
            var start = cursor.Offset;

            cursor.ResetHitEnd();

            if (_skipWhiteSpace)
            {
                // A white space parser can throw too, for a comment which the window doesn't end
                try
                {
                    context.SkipWhiteSpace();
                }
                catch (ParseException) when (cursor.HitEnd)
                {
                    RetryOffset = start;
                    return StreamingStep.NeedMore;
                }
                catch (ParseException e)
                {
                    throw _driver.ToAbsolute(e);
                }

                // Don't retain the white space unless what follows the window can change how it is read: a white space
                // parser reads a comment which the window cuts again from its start. The chars of the context's set are
                // white space whatever follows them, so a run of them is dropped window after window
                if (!cursor.HitEnd || context.WhiteSpaceParser is null)
                {
                    start = cursor.Offset;
                }
            }

            if (cursor.Eof)
            {
                // A trailing separator is accepted
                if (_driver.IsFinal)
                {
                    return StreamingStep.End;
                }

                RetryOffset = start;
                return StreamingStep.NeedMore;
            }

            if (_expectEnd)
            {
                if (cursor.HitEnd)
                {
                    RetryOffset = start;
                    return StreamingStep.NeedMore;
                }

                throw new ParseException("Expected a separator or the end of the input.", _driver.ToAbsolute(cursor.Position));
            }

            var itemStart = cursor.Offset;

            if (_expectSeparator)
            {
                var separatorResult = new ParseResult<TSeparator>();
                bool separated;

                try
                {
                    separated = _separator!.Parse(context, ref separatorResult);
                }
                catch (ParseException) when (cursor.HitEnd)
                {
                    RetryOffset = start;
                    return StreamingStep.NeedMore;
                }
                catch (ParseException e)
                {
                    throw _driver.ToAbsolute(e);
                }

                if (cursor.HitEnd)
                {
                    RetryOffset = start;
                    return StreamingStep.NeedMore;
                }

                _expectSeparator = false;
                _expectEnd = !separated;
                continue;
            }

            var result = new ParseResult<T>();
            bool success;

            try
            {
                success = _parser.Parse(context, ref result);
            }
            catch (ParseException) when (cursor.HitEnd)
            {
                RetryOffset = start;
                return StreamingStep.NeedMore;
            }
            catch (ParseException e)
            {
                throw _driver.ToAbsolute(e);
            }

            if (cursor.HitEnd)
            {
                RetryOffset = start;
                return StreamingStep.NeedMore;
            }

            if (!success)
            {
                throw new ParseException("The input could not be parsed.", _driver.ToAbsolute(cursor.GetPosition(itemStart)));
            }

            if (cursor.Offset == itemStart)
            {
                throw new InvalidOperationException("The parser succeeded without consuming any input, which would never end.");
            }

            _expectSeparator = _separator != null;
            value = result.Value;
            return StreamingStep.Item;
        }
    }
}

internal enum StreamingStep
{
    Item,
    End,
    NeedMore,
}
