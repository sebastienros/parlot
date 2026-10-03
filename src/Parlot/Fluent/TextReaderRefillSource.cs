using System;
using System.Buffers;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Parlot.Fluent;

/// <summary>
/// Reads a <see cref="TextReader"/> into the compacting buffer of a cursor.
/// </summary>
/// <remarks>
/// Each refill drops the text before the backtrack floor and builds a new immutable window with the retained text and the
/// text read next. Results referencing previous windows remain valid. The retained text is bounded by the largest token
/// or pinned region, not by the size of the input.
/// </remarks>
internal sealed class TextReaderRefillSource : StreamRefillSource, IDisposable
{
    private readonly TextReader _reader;
    private readonly int _bufferSize;
    private readonly int _maxBufferedCharacters;
    private readonly bool _readAsynchronously;

    private char[] _chars = [];

    /// <param name="reader">The reader.</param>
    /// <param name="options">The options.</param>
    /// <param name="readAsynchronously">
    /// Whether refills block on <see cref="TextReader.ReadAsync(char[], int, int)"/> rather than calling <see cref="TextReader.Read(char[], int, int)"/>,
    /// for readers over streams which don't support synchronous reads.
    /// </param>
    public TextReaderRefillSource(TextReader reader, StreamParseOptions options, bool readAsynchronously = false)
    {
        _reader = reader;
        _readAsynchronously = readAsynchronously;
        _bufferSize = options.BufferSize;
        _maxBufferedCharacters = options.MaxBufferedCharacters;

        if (_bufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "BufferSize must be positive.");
        }

        if (_maxBufferedCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxBufferedCharacters must be positive.");
        }
    }

    /// <summary>
    /// Reads the first window.
    /// </summary>
    /// <returns>The text read, and whether it is the whole input.</returns>
    public string ReadFirst(CancellationToken cancellationToken, out bool isFinal)
    {
        return Read(null, 0, 0, cancellationToken, out isFinal);
    }

    /// <summary>
    /// Reads the first window asynchronously.
    /// </summary>
    /// <returns>The text read, and whether it is the whole input.</returns>
    public async ValueTask<(string Text, bool IsFinal)> ReadFirstAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var target = Math.Min(_bufferSize, _maxBufferedCharacters);
        _chars = ArrayPool<char>.Shared.Rent(target);

        var length = 0;
        var isFinal = false;

        while (length < target)
        {
#if NET8_0_OR_GREATER
            var read = await _reader.ReadAsync(_chars.AsMemory(length, target - length), cancellationToken).ConfigureAwait(false);
#else
            var read = await _reader.ReadAsync(_chars, length, target - length).ConfigureAwait(false);
#endif

            if (read == 0)
            {
                isFinal = true;
                break;
            }

            length += read;
        }

        PeakBufferedCharacters = length;

        return (new string(_chars, 0, length), isFinal);
    }

    public override void Refill(Cursor cursor, int floor, CancellationToken cancellationToken)
    {
        var buffer = cursor.Buffer;
        var bufferStart = cursor.BufferStart;
        var keepStart = floor - bufferStart;
        var keep = buffer.Length - keepStart;

        if (keep >= _maxBufferedCharacters)
        {
            throw new ParseException($"The input requires more than {_maxBufferedCharacters} buffered characters to be parsed.", cursor.Position);
        }

        var text = Read(buffer, keepStart, keep, cancellationToken, out var isFinal);

        if ((long)floor + text.Length > int.MaxValue)
        {
            throw new ParseException($"Streamed inputs larger than {int.MaxValue} characters are not supported.", cursor.Position);
        }

        cursor.ReplaceBuffer(text, floor, isFinal);
    }

    /// <summary>
    /// The largest window built, in chars.
    /// </summary>
    public int PeakBufferedCharacters { get; private set; }

    private string Read(string? retained, int retainedStart, int retainedLength, CancellationToken cancellationToken, out bool isFinal)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Read at least as much as is retained so that copying the retained text costs O(1) per char read
        var target = retainedLength + Math.Max(retainedLength, _bufferSize);

        if (target < 0 || target > _maxBufferedCharacters)
        {
            target = _maxBufferedCharacters;
        }

        if (_chars.Length < target)
        {
            ReturnChars();
            _chars = ArrayPool<char>.Shared.Rent(target);
        }

        retained?.CopyTo(retainedStart, _chars, 0, retainedLength);

        var length = retainedLength;
        isFinal = false;

        while (length < target)
        {
            var read = _readAsynchronously ? ReadBlocking(length, target - length, cancellationToken) : _reader.Read(_chars, length, target - length);

            if (read == 0)
            {
                isFinal = true;
                break;
            }

            length += read;
        }

        if (length > PeakBufferedCharacters)
        {
            PeakBufferedCharacters = length;
        }

        return new string(_chars, 0, length);
    }

    private int ReadBlocking(int index, int count, CancellationToken cancellationToken)
    {
#if NET8_0_OR_GREATER
        var read = _reader.ReadAsync(_chars.AsMemory(index, count), cancellationToken);
        return read.IsCompletedSuccessfully ? read.Result : read.AsTask().GetAwaiter().GetResult();
#else
        cancellationToken.ThrowIfCancellationRequested();
        return _reader.ReadAsync(_chars, index, count).GetAwaiter().GetResult();
#endif
    }

    private void ReturnChars()
    {
        if (_chars.Length > 0)
        {
            ArrayPool<char>.Shared.Return(_chars);
            _chars = [];
        }
    }

    public void Dispose()
    {
        ReturnChars();
    }
}
