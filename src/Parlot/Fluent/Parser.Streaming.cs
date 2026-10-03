using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
#if NET8_0_OR_GREATER
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#endif

namespace Parlot.Fluent;

public abstract partial class Parser<T>
{
    /// <summary>
    /// Parses the text of a <see cref="TextReader"/>, buffering only the text the parser can still read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The result is the one of <see cref="Parse(string)"/> on the whole text. The text which follows the result is not
    /// necessarily read. A <see cref="ParseException"/> thrown by the parser is propagated.
    /// Positions are the ones in the whole text.
    /// </para>
    /// <para>
    /// The first block is read asynchronously. When the text doesn't fit in it, the parse runs on a thread pool thread,
    /// through the compacting buffer of <see cref="Parse(TextReader, StreamParseOptions?, CancellationToken)"/>, and that thread
    /// blocks on the asynchronous reads of the next blocks.
    /// </para>
    /// </remarks>
    /// <returns>The parsed value, or <see langword="default"/> if the text doesn't match.</returns>
    public async ValueTask<T?> ParseAsync(TextReader reader, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        var (_, value) = await ParseStreamAsync(reader, options, cancellationToken).ConfigureAwait(false);
        return value;
    }

    /// <summary>
    /// Parses the text of a <see cref="Stream"/>, buffering only the text the parser can still read.
    /// </summary>
    /// <remarks>
    /// The stream is decoded with <see cref="StreamParseOptions.Encoding"/> and is not closed.
    /// See <see cref="ParseAsync(TextReader, StreamParseOptions?, CancellationToken)"/>.
    /// </remarks>
    /// <returns>The parsed value, or <see langword="default"/> if the text doesn't match.</returns>
    public async ValueTask<T?> ParseAsync(Stream stream, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var reader = StreamingDriver.CreateReader(stream, options);
        return await ParseAsync(reader, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses the text of a <see cref="TextReader"/>, buffering only the text the parser can still read.
    /// </summary>
    /// <remarks>
    /// The result is the one of <see cref="TryParse(string, out T)"/> on the whole text, except that cancellation throws an
    /// <see cref="OperationCanceledException"/>. The text which follows the result is not necessarily read.
    /// See <see cref="ParseAsync(TextReader, StreamParseOptions?, CancellationToken)"/>.
    /// </remarks>
    /// <returns>Whether the text matched, and the parsed value.</returns>
    public async ValueTask<(bool Success, T? Value)> TryParseAsync(TextReader reader, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        try
        {
            return await ParseStreamAsync(reader, options, cancellationToken).ConfigureAwait(false);
        }
        catch (ParseException)
        {
            return (false, default);
        }
    }

    /// <summary>
    /// Parses the text of a <see cref="Stream"/>, buffering only the text the parser can still read.
    /// </summary>
    /// <remarks>
    /// The stream is decoded with <see cref="StreamParseOptions.Encoding"/> and is not closed.
    /// See <see cref="TryParseAsync(TextReader, StreamParseOptions?, CancellationToken)"/>.
    /// </remarks>
    /// <returns>Whether the text matched, and the parsed value.</returns>
    public async ValueTask<(bool Success, T? Value)> TryParseAsync(Stream stream, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(stream, nameof(stream));

        using var reader = StreamingDriver.CreateReader(stream, options);

        // The parse would most likely read a small seekable stream to its end
        if (stream.CanSeek && stream.Length - stream.Position <= (options ?? StreamParseOptions.Default).MaxBufferedCharacters)
        {
            cancellationToken.ThrowIfCancellationRequested();

#if NET8_0_OR_GREATER
            var text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
#else
            var text = await reader.ReadToEndAsync().ConfigureAwait(false);
#endif

            try
            {
                return ParseText(text, options, cancellationToken);
            }
            catch (ParseException)
            {
                return (false, default);
            }
        }

        return await TryParseAsync(reader, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses successive values from a <see cref="TextReader"/> and invokes <paramref name="onItem"/> for each of them,
    /// retaining only the text of the value being parsed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// White space is skipped before each value and before the end, see <see cref="StreamParseOptions.SkipWhiteSpace"/>.
    /// The text must only contain values. A value which doesn't match throws a <see cref="ParseException"/> with the
    /// position of the value in the whole text, and a value matching an empty text throws an <see cref="InvalidOperationException"/>.
    /// </para>
    /// <para>
    /// <paramref name="onItem"/> is invoked once a value is complete. Callbacks invoked while parsing, for instance in
    /// <c>Then</c>, can be invoked more than once for the same text, when more text is needed to complete a value.
    /// The <see cref="TextSpan"/> values of results remain valid, but their offsets are relative to their <see cref="TextSpan.Buffer"/>,
    /// not to the whole text. Lines and columns are relative to the whole text.
    /// </para>
    /// </remarks>
    /// <returns>The number of parsed values.</returns>
    public ValueTask<long> ParseManyAsync(TextReader reader, Func<T, CancellationToken, ValueTask> onItem, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(onItem, nameof(onItem));
        return ParseManyCoreAsync<TextSpan>(reader, null, onItem, null, options, cancellationToken);
    }

    /// <inheritdoc cref="ParseManyAsync(TextReader, Func{T, CancellationToken, ValueTask}, StreamParseOptions?, CancellationToken)"/>
    public ValueTask<long> ParseManyAsync(TextReader reader, Action<T> onItem, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(onItem, nameof(onItem));
        return ParseManyCoreAsync<TextSpan>(reader, null, null, onItem, options, cancellationToken);
    }

    /// <summary>
    /// Parses successive values separated by <paramref name="separator"/> from a <see cref="TextReader"/> and invokes
    /// <paramref name="onItem"/> for each of them, retaining only the text of the value being parsed.
    /// </summary>
    /// <remarks>
    /// A trailing separator is accepted. When the separator doesn't match, only white space can follow.
    /// See <see cref="ParseManyAsync(TextReader, Func{T, CancellationToken, ValueTask}, StreamParseOptions?, CancellationToken)"/>.
    /// </remarks>
    /// <returns>The number of parsed values.</returns>
    public ValueTask<long> ParseManyAsync<TSeparator>(TextReader reader, Parser<TSeparator> separator, Func<T, CancellationToken, ValueTask> onItem, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(separator, nameof(separator));
        ThrowHelper.ThrowIfNull(onItem, nameof(onItem));
        return ParseManyCoreAsync(reader, separator, onItem, null, options, cancellationToken);
    }

    /// <inheritdoc cref="ParseManyAsync(TextReader, Func{T, CancellationToken, ValueTask}, StreamParseOptions?, CancellationToken)"/>
    /// <remarks>The stream is decoded with <see cref="StreamParseOptions.Encoding"/> and is not closed.</remarks>
    public async ValueTask<long> ParseManyAsync(Stream stream, Func<T, CancellationToken, ValueTask> onItem, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var reader = StreamingDriver.CreateReader(stream, options);
        return await ParseManyAsync(reader, onItem, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ParseManyAsync(TextReader, Func{T, CancellationToken, ValueTask}, StreamParseOptions?, CancellationToken)"/>
    /// <remarks>The stream is decoded with <see cref="StreamParseOptions.Encoding"/> and is not closed.</remarks>
    public async ValueTask<long> ParseManyAsync(Stream stream, Action<T> onItem, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var reader = StreamingDriver.CreateReader(stream, options);
        return await ParseManyAsync(reader, onItem, options, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc cref="ParseManyAsync{TSeparator}(TextReader, Parser{TSeparator}, Func{T, CancellationToken, ValueTask}, StreamParseOptions?, CancellationToken)"/>
    /// <remarks>The stream is decoded with <see cref="StreamParseOptions.Encoding"/> and is not closed.</remarks>
    public async ValueTask<long> ParseManyAsync<TSeparator>(Stream stream, Parser<TSeparator> separator, Func<T, CancellationToken, ValueTask> onItem, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var reader = StreamingDriver.CreateReader(stream, options);
        return await ParseManyAsync(reader, separator, onItem, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Parses each text delimited by <paramref name="delimiter"/> in a <see cref="TextReader"/> as a value and invokes
    /// <paramref name="onItem"/> for each of them, for instance one value per line with <c>'\n'</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The text is read until a delimiter is found, so a value is parsed as soon as its delimiter is received. This suits
    /// interactive sources, while the other overloads may wait for more text than the value needs.
    /// Each delimited text is parsed on its own and must be entirely matched by the parser, except for white space when
    /// <see cref="StreamParseOptions.SkipWhiteSpace"/> is enabled. Empty texts are ignored, and white space only texts too
    /// when <see cref="StreamParseOptions.SkipWhiteSpace"/> is enabled.
    /// </para>
    /// <para>
    /// A text which doesn't match throws a <see cref="ParseException"/> with the position in the whole text. A text longer
    /// than <see cref="StreamParseOptions.MaxBufferedCharacters"/> throws a <see cref="ParseException"/>.
    /// The <see cref="TextSpan"/> values of results remain valid, but their offsets are relative to their <see cref="TextSpan.Buffer"/>,
    /// not to the whole text. Lines and columns are relative to the whole text.
    /// </para>
    /// </remarks>
    /// <returns>The number of parsed values.</returns>
    public ValueTask<long> ParseManyAsync(TextReader reader, char delimiter, Func<T, CancellationToken, ValueTask> onItem, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(reader, nameof(reader));
        ThrowHelper.ThrowIfNull(onItem, nameof(onItem));
        return ParseFramesCoreAsync(reader, delimiter, onItem, null, options, cancellationToken);
    }

    /// <inheritdoc cref="ParseManyAsync(TextReader, char, Func{T, CancellationToken, ValueTask}, StreamParseOptions?, CancellationToken)"/>
    public ValueTask<long> ParseManyAsync(TextReader reader, char delimiter, Action<T> onItem, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(reader, nameof(reader));
        ThrowHelper.ThrowIfNull(onItem, nameof(onItem));
        return ParseFramesCoreAsync(reader, delimiter, null, onItem, options, cancellationToken);
    }

    /// <inheritdoc cref="ParseManyAsync(TextReader, char, Func{T, CancellationToken, ValueTask}, StreamParseOptions?, CancellationToken)"/>
    /// <remarks>The stream is decoded with <see cref="StreamParseOptions.Encoding"/> and is not closed.</remarks>
    public async ValueTask<long> ParseManyAsync(Stream stream, char delimiter, Func<T, CancellationToken, ValueTask> onItem, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var reader = StreamingDriver.CreateReader(stream, options);
        return await ParseManyAsync(reader, delimiter, onItem, options, cancellationToken).ConfigureAwait(false);
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// Parses successive values from a <see cref="TextReader"/>, retaining only the text of the value being parsed.
    /// </summary>
    /// <remarks>See <see cref="ParseManyAsync(TextReader, Func{T, CancellationToken, ValueTask}, StreamParseOptions?, CancellationToken)"/>.</remarks>
    public IAsyncEnumerable<T> ParseManyAsync(TextReader reader, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(reader, nameof(reader));
        return EnumerateAsync<TextSpan>(reader, null, null, options, cancellationToken);
    }

    /// <summary>
    /// Parses successive values separated by <paramref name="separator"/> from a <see cref="TextReader"/>, retaining only the text of the value being parsed.
    /// </summary>
    /// <remarks>See <see cref="ParseManyAsync{TSeparator}(TextReader, Parser{TSeparator}, Func{T, CancellationToken, ValueTask}, StreamParseOptions?, CancellationToken)"/>.</remarks>
    public IAsyncEnumerable<T> ParseManyAsync<TSeparator>(TextReader reader, Parser<TSeparator> separator, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(reader, nameof(reader));
        ThrowHelper.ThrowIfNull(separator, nameof(separator));
        return EnumerateAsync(reader, null, separator, options, cancellationToken);
    }

    /// <summary>
    /// Parses successive values from a <see cref="Stream"/>, retaining only the text of the value being parsed.
    /// </summary>
    /// <remarks>
    /// The stream is decoded with <see cref="StreamParseOptions.Encoding"/> and is not closed.
    /// See <see cref="ParseManyAsync(TextReader, Func{T, CancellationToken, ValueTask}, StreamParseOptions?, CancellationToken)"/>.
    /// </remarks>
    public IAsyncEnumerable<T> ParseManyAsync(Stream stream, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(stream, nameof(stream));
        return EnumerateAsync<TextSpan>(null, stream, null, options, cancellationToken);
    }

    /// <summary>
    /// Parses successive values separated by <paramref name="separator"/> from a <see cref="Stream"/>, retaining only the text of the value being parsed.
    /// </summary>
    /// <remarks>
    /// The stream is decoded with <see cref="StreamParseOptions.Encoding"/> and is not closed.
    /// See <see cref="ParseManyAsync{TSeparator}(TextReader, Parser{TSeparator}, Func{T, CancellationToken, ValueTask}, StreamParseOptions?, CancellationToken)"/>.
    /// </remarks>
    public IAsyncEnumerable<T> ParseManyAsync<TSeparator>(Stream stream, Parser<TSeparator> separator, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(stream, nameof(stream));
        ThrowHelper.ThrowIfNull(separator, nameof(separator));
        return EnumerateAsync(null, stream, separator, options, cancellationToken);
    }

    /// <summary>
    /// Parses each text delimited by <paramref name="delimiter"/> in a <see cref="TextReader"/> as a value.
    /// </summary>
    /// <remarks>See <see cref="ParseManyAsync(TextReader, char, Func{T, CancellationToken, ValueTask}, StreamParseOptions?, CancellationToken)"/>.</remarks>
    public IAsyncEnumerable<T> ParseManyAsync(TextReader reader, char delimiter, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(reader, nameof(reader));
        return EnumerateFramesAsync(reader, null, delimiter, options, cancellationToken);
    }

    /// <summary>
    /// Parses each text delimited by <paramref name="delimiter"/> in a <see cref="Stream"/> as a value.
    /// </summary>
    /// <remarks>
    /// The stream is decoded with <see cref="StreamParseOptions.Encoding"/> and is not closed.
    /// See <see cref="ParseManyAsync(TextReader, char, Func{T, CancellationToken, ValueTask}, StreamParseOptions?, CancellationToken)"/>.
    /// </remarks>
    public IAsyncEnumerable<T> ParseManyAsync(Stream stream, char delimiter, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(stream, nameof(stream));
        return EnumerateFramesAsync(null, stream, delimiter, options, cancellationToken);
    }

    private async IAsyncEnumerable<T> EnumerateFramesAsync(TextReader? reader, Stream? stream, char delimiter, StreamParseOptions? options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var ownedReader = stream is null ? null : StreamingDriver.CreateReader(stream, options);
        using var driver = new StreamingDriver(reader ?? ownedReader!, options, cancellationToken);

        while (await driver.NextFrameAsync(delimiter).ConfigureAwait(false))
        {
            if (TryParseFrame(driver, out var value))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return value;
            }
        }
    }

    private async IAsyncEnumerable<T> EnumerateAsync<TSeparator>(TextReader? reader, Stream? stream, Parser<TSeparator>? separator, StreamParseOptions? options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var ownedReader = stream is null ? null : StreamingDriver.CreateReader(stream, options);
        using var driver = new StreamingDriver(reader ?? ownedReader!, options, cancellationToken);
        var items = new StreamingItemReader<T, TSeparator>(driver, this, separator);

        while (true)
        {
            switch (items.Next(out var value))
            {
                case StreamingStep.Item:
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return value;
                    break;
                case StreamingStep.End:
                    yield break;
                default:
                    await driver.GrowAsync(items.RetryPosition).ConfigureAwait(false);
                    break;
            }
        }
    }
#endif

    private async ValueTask<long> ParseManyCoreAsync<TSeparator>(TextReader reader, Parser<TSeparator>? separator, Func<T, CancellationToken, ValueTask>? onItemAsync, Action<T>? onItem, StreamParseOptions? options, CancellationToken cancellationToken)
    {
        using var driver = new StreamingDriver(reader, options, cancellationToken);
        var items = new StreamingItemReader<T, TSeparator>(driver, this, separator);

        while (true)
        {
            switch (items.Next(out var value))
            {
                case StreamingStep.Item:
                    cancellationToken.ThrowIfCancellationRequested();

                    if (onItem != null)
                    {
                        onItem(value);
                    }
                    else
                    {
                        await onItemAsync!(value, cancellationToken).ConfigureAwait(false);
                    }

                    break;
                case StreamingStep.End:
                    return items.Count;
                default:
                    await driver.GrowAsync(items.RetryPosition).ConfigureAwait(false);
                    break;
            }
        }
    }

    private async ValueTask<long> ParseFramesCoreAsync(TextReader reader, char delimiter, Func<T, CancellationToken, ValueTask>? onItemAsync, Action<T>? onItem, StreamParseOptions? options, CancellationToken cancellationToken)
    {
        using var driver = new StreamingDriver(reader, options, cancellationToken);
        long count = 0;

        while (await driver.NextFrameAsync(delimiter).ConfigureAwait(false))
        {
            if (!TryParseFrame(driver, out var value))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            count++;

            if (onItem != null)
            {
                onItem(value);
            }
            else
            {
                await onItemAsync!(value, cancellationToken).ConfigureAwait(false);
            }
        }

        return count;
    }

    /// <summary>
    /// Parses the current frame entirely, leaving the cursor at its end.
    /// </summary>
    /// <returns><see langword="false"/> if the frame only contains white space.</returns>
    private bool TryParseFrame(StreamingDriver driver, out T value)
    {
        var context = driver.Context;
        var cursor = context.Scanner.Cursor;
        var skipWhiteSpace = driver.Options.SkipWhiteSpace;

        value = default!;

        if (skipWhiteSpace)
        {
            context.SkipWhiteSpace();

            if (cursor.Eof)
            {
                return false;
            }
        }

        var start = cursor.Position;
        var result = new ParseResult<T>();
        bool success;

        try
        {
            success = Parse(context, ref result);
        }
        catch (ParseException e)
        {
            throw driver.ToAbsolute(e);
        }

        if (!success)
        {
            throw new ParseException("The input could not be parsed.", driver.ToAbsolute(start));
        }

        if (skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        if (!cursor.Eof)
        {
            throw new ParseException("Expected the end of the delimited text.", driver.ToAbsolute(cursor.Position));
        }

        value = result.Value;
        return true;
    }

    private async ValueTask<(bool Success, T? Value)> ParseStreamAsync(TextReader reader, StreamParseOptions? options, CancellationToken cancellationToken)
    {
        ThrowHelper.ThrowIfNull(reader, nameof(reader));

        // The text is already in memory
        if (reader is StringReader && (options ?? StreamParseOptions.Default).MaxBufferedCharacters == int.MaxValue)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ParseText(reader.ReadToEnd(), options, cancellationToken);
        }

        return await ParseCompactingAsync(reader, options, cancellationToken).ConfigureAwait(false);
    }

    private (bool Success, T? Value) ParseText(string text, StreamParseOptions? options, CancellationToken cancellationToken)
    {
        var scanner = new Scanner(text);
        var context = options?.ContextFactory?.Invoke(scanner, cancellationToken) ?? new ParseContext(scanner, cancellationToken);
        var result = new ParseResult<T>();

        return Parse(context, ref result) ? (true, result.Value) : (false, default);
    }
}
