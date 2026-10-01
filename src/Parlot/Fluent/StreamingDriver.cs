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
        Context = CreateContext("");
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
    public async ValueTask GrowAsync(TextPosition start)
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

        Context = CreateContext(new string(_chars, 0, _length));
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

    private ParseContext CreateContext(string text)
    {
        var scanner = new Scanner(text, new TextPosition(0, _line, _column), IsFinal);
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
