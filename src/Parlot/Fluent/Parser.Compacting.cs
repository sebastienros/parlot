using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Parlot.Fluent;

public abstract partial class Parser<T>
{
    /// <summary>
    /// Parses the text of a <see cref="TextReader"/>, buffering only the text the parser can still read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The result is the one of <see cref="Parse(string)"/> on the whole text, and positions are the ones in the whole text.
    /// The text which follows the result is not necessarily read. A <see cref="ParseException"/> thrown by the parser is propagated.
    /// </para>
    /// <para>
    /// The text is read in blocks of <see cref="StreamParseOptions.BufferSize"/> chars into a compacting buffer, which drops the text
    /// no parser can move back to. Its size is bounded by the largest token or alternative being parsed, not by the size of the input.
    /// See <see cref="ParseContext.IsCompacting"/> for the requirements on custom parsers, and <see cref="Commit"/>.
    /// </para>
    /// </remarks>
    /// <returns>The parsed value, or <see langword="default"/> if the text doesn't match.</returns>
    public T? Parse(TextReader reader, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(reader, nameof(reader));

        options ??= StreamParseOptions.Default;

        using var source = new TextReaderRefillSource(reader, options);
        var text = source.ReadFirst(cancellationToken, out var isFinal);
        var context = CreateCompactingContext(source, text, isFinal, options, cancellationToken);

        return ParseCompacting(context, out var value) ? value : default;
    }

    /// <summary>
    /// Parses the text of a <see cref="Stream"/>, buffering only the text the parser can still read.
    /// </summary>
    /// <remarks>
    /// The stream is decoded with <see cref="StreamParseOptions.Encoding"/> and is not closed.
    /// See <see cref="Parse(TextReader, StreamParseOptions?, CancellationToken)"/>.
    /// </remarks>
    /// <returns>The parsed value, or <see langword="default"/> if the text doesn't match.</returns>
    public T? Parse(Stream stream, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var reader = StreamingDriver.CreateReader(stream, options);
        return Parse(reader, options, cancellationToken);
    }

    /// <summary>
    /// Parses the text of a <see cref="TextReader"/>, buffering only the text the parser can still read.
    /// </summary>
    /// <remarks>
    /// The result is the one of <see cref="TryParse(string, out T, out ParseError?)"/> on the whole text.
    /// See <see cref="Parse(TextReader, StreamParseOptions?, CancellationToken)"/>.
    /// </remarks>
    /// <returns>Whether the text matched.</returns>
    public bool TryParse(TextReader reader, out T value, out ParseError? error, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        ThrowHelper.ThrowIfNull(reader, nameof(reader));

        options ??= StreamParseOptions.Default;
        error = null;

        using var source = new TextReaderRefillSource(reader, options);

        ParseContext? context = null;

        try
        {
            var text = source.ReadFirst(cancellationToken, out var isFinal);
            context = CreateCompactingContext(source, text, isFinal, options, cancellationToken);

            if (ParseCompacting(context, out var parsed))
            {
                value = parsed!;
                return true;
            }
        }
        catch (ParseException e)
        {
            error = new ParseError { Message = e.Message, Position = e.Position };
        }
        catch (OperationCanceledException e)
        {
            error = new ParseError { Message = e.Message, Position = context?.Scanner.Cursor.Position ?? TextPosition.Start };
        }

        value = default!;
        return false;
    }

    /// <summary>
    /// Parses the text of a <see cref="TextReader"/>, buffering only the text the parser can still read.
    /// </summary>
    /// <remarks>
    /// See <see cref="TryParse(TextReader, out T, out ParseError?, StreamParseOptions?, CancellationToken)"/>.
    /// </remarks>
    /// <returns>Whether the text matched.</returns>
    public bool TryParse(TextReader reader, out T value, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        return TryParse(reader, out value, out _, options, cancellationToken);
    }

    /// <summary>
    /// Parses the text of a <see cref="Stream"/>, buffering only the text the parser can still read.
    /// </summary>
    /// <remarks>
    /// The stream is decoded with <see cref="StreamParseOptions.Encoding"/> and is not closed.
    /// See <see cref="TryParse(TextReader, out T, out ParseError?, StreamParseOptions?, CancellationToken)"/>.
    /// </remarks>
    /// <returns>Whether the text matched.</returns>
    public bool TryParse(Stream stream, out T value, out ParseError? error, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var reader = StreamingDriver.CreateReader(stream, options);
        return TryParse(reader, out value, out error, options, cancellationToken);
    }

    /// <summary>
    /// Parses the text of a <see cref="Stream"/>, buffering only the text the parser can still read.
    /// </summary>
    /// <remarks>
    /// See <see cref="TryParse(Stream, out T, out ParseError?, StreamParseOptions?, CancellationToken)"/>.
    /// </remarks>
    /// <returns>Whether the text matched.</returns>
    public bool TryParse(Stream stream, out T value, StreamParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        return TryParse(stream, out value, out _, options, cancellationToken);
    }

    /// <summary>
    /// Creates a context on <paramref name="text"/>, the first block read by <paramref name="source"/>, which refills it.
    /// </summary>
    private static ParseContext CreateCompactingContext(TextReaderRefillSource source, string text, bool isFinal, StreamParseOptions options, CancellationToken cancellationToken)
    {
        var scanner = new Scanner(text, isFinal);
        var context = options.ContextFactory?.Invoke(scanner, cancellationToken) ?? new ParseContext(scanner, cancellationToken);

        if (context.Scanner != scanner)
        {
            throw new InvalidOperationException("The ContextFactory must return a context using the provided scanner.");
        }

        if (!isFinal)
        {
            context.StartCompacting(source);
        }

        return context;
    }

    /// <summary>
    /// Reads the first block asynchronously, then parses on a thread pool thread if the text doesn't fit in it.
    /// </summary>
    /// <remarks>
    /// The parse is synchronous, so the refills block that thread on <see cref="TextReader.ReadAsync(char[], int, int)"/>.
    /// </remarks>
    private async ValueTask<(bool Success, T? Value)> ParseCompactingAsync(TextReader reader, StreamParseOptions? options, CancellationToken cancellationToken)
    {
        options ??= StreamParseOptions.Default;

        using var source = new TextReaderRefillSource(reader, options, readAsynchronously: true);
        var (text, isFinal) = await source.ReadFirstAsync(cancellationToken).ConfigureAwait(false);
        var context = CreateCompactingContext(source, text, isFinal, options, cancellationToken);

        if (isFinal)
        {
            return ParseCompacting(context);
        }

        return await Task.Run(() => ParseCompacting(context), cancellationToken).ConfigureAwait(false);
    }

    private (bool Success, T? Value) ParseCompacting(ParseContext context)
    {
        return ParseCompacting(context, out var value) ? (true, value) : (false, default);
    }

    private bool ParseCompacting(ParseContext context, out T? value)
    {
        try
        {
            var result = new ParseResult<T>();
            var success = Parse(context, ref result);

            context.CheckCompactingEnd(success);

            value = success ? result.Value : default;
            return success;
        }
        finally
        {
            context.StopCompacting();
        }
    }
}
