using System;
using System.Buffers;
using System.IO;
using System.Text;
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

        if (_options.BufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "BufferSize must be positive.");
        }

        if (_options.MaxBufferedCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxBufferedCharacters must be positive.");
        }

        _chars = [];
        Context = CreateContext("", isFinal: false);
    }

    public StreamParseOptions Options => _options;

    /// <summary>
    /// The context for the current window.
    /// </summary>
    public ParseContext Context { get; private set; }

    public Cursor Cursor => Context.Scanner.Cursor;

    /// <summary>
    /// Whether the window holds the end of the input.
    /// </summary>
    public bool IsFinal { get; private set; }

    public static StreamReader CreateReader(Stream stream, StreamParseOptions? options)
    {
        ThrowHelper.ThrowIfNull(stream, nameof(stream));

        var bufferSize = options?.BufferSize ?? StreamParseOptions.Default.BufferSize;
        bufferSize = bufferSize < 128 ? 128 : bufferSize > 81920 ? 81920 : bufferSize;

        return new StreamReader(stream, options?.Encoding ?? new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true, bufferSize, leaveOpen: true);
    }

    /// <summary>
    /// Drops the text before <paramref name="start"/>, reads more text and starts a new window at <paramref name="start"/>.
    /// </summary>
    /// <param name="start">The position of the text to keep.</param>
    /// <param name="growthFactor">How many times larger than the kept text the new window should be, at least 2.</param>
    public async ValueTask GrowAsync(TextPosition start, int growthFactor = 2)
    {
        var available = _length - start.Offset;

        if (available >= _options.MaxBufferedCharacters)
        {
            throw new ParseException($"The input requires more than {_options.MaxBufferedCharacters} buffered characters to be parsed.", ToAbsolute(start));
        }

        if (start.Offset > 0)
        {
            Array.Copy(_chars, start.Offset, _chars, 0, available);
            _length = available;
            _offset += start.Offset;
        }

        _line = start.Line;
        _column = start.Column;

        // Grow geometrically so that the retries cost a bounded multiple of a single parse
        var target = available + Math.Max(available * (growthFactor - 1), _options.BufferSize);

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

                // The cursor doesn't count a column for the character before a '\r'
                if (_frameDelimited && delimiter == '\r' && _frameLength > 0)
                {
                    _column--;
                }

                var consumed = _frameLength + (_frameDelimited ? 1 : 0);
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
                return true;
            }

            // Skip empty frames, the window is empty
            Context = CreateContext("", isFinal: true);
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

        if (delimiter == '\n')
        {
            _line++;
            _column = 1;
        }
        else if (_frameStart >= _length || _chars[_frameStart] != '\r')
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
        exception.Position = ToAbsolute(exception.Position);
        return exception;
    }

    private ParseContext CreateContext(string text, bool isFinal)
    {
        var scanner = new Scanner(text, new TextPosition(0, _line, _column), isFinal);
        var context = _options.ContextFactory?.Invoke(scanner, _cancellationToken) ?? new ParseContext(scanner, _cancellationToken);

        if (context.Scanner != scanner)
        {
            throw new InvalidOperationException("The ContextFactory must return a context using the provided scanner.");
        }

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

    public long Count { get; private set; }

    /// <summary>
    /// The position to retry from when <see cref="Next(out T)"/> returns <see cref="StreamingStep.NeedMore"/>.
    /// </summary>
    public TextPosition RetryPosition { get; private set; }

    public StreamingStep Next(out T value)
    {
        value = default!;

        while (true)
        {
            var context = _driver.Context;
            var cursor = context.Scanner.Cursor;
            var start = cursor.Position;

            cursor.ResetHitEnd();

            if (_skipWhiteSpace)
            {
                context.SkipWhiteSpace();

                // Don't retain the white space unless it might continue
                if (!cursor.HitEnd)
                {
                    start = cursor.Position;
                }
            }

            if (cursor.Eof)
            {
                // A trailing separator is accepted
                if (_driver.IsFinal)
                {
                    return StreamingStep.End;
                }

                RetryPosition = start;
                return StreamingStep.NeedMore;
            }

            if (_expectEnd)
            {
                if (cursor.HitEnd)
                {
                    RetryPosition = start;
                    return StreamingStep.NeedMore;
                }

                throw new ParseException("Expected a separator or the end of the input.", _driver.ToAbsolute(cursor.Position));
            }

            var itemStart = cursor.Position;

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
                    RetryPosition = start;
                    return StreamingStep.NeedMore;
                }
                catch (ParseException e)
                {
                    throw _driver.ToAbsolute(e);
                }

                if (cursor.HitEnd)
                {
                    RetryPosition = start;
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
                RetryPosition = start;
                return StreamingStep.NeedMore;
            }
            catch (ParseException e)
            {
                throw _driver.ToAbsolute(e);
            }

            if (cursor.HitEnd)
            {
                RetryPosition = start;
                return StreamingStep.NeedMore;
            }

            if (!success)
            {
                throw new ParseException("The input could not be parsed.", _driver.ToAbsolute(itemStart));
            }

            if (cursor.Offset == itemStart.Offset)
            {
                throw new InvalidOperationException("The parser succeeded without consuming any input, which would never end.");
            }

            _expectSeparator = _separator != null;
            Count++;
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
