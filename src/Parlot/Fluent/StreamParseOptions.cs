using System;
using System.Text;
using System.Threading;

namespace Parlot.Fluent;

/// <summary>
/// Options for parsing a <see cref="System.IO.TextReader"/> or a <see cref="System.IO.Stream"/>.
/// </summary>
public sealed class StreamParseOptions
{
    internal static readonly StreamParseOptions Default = new();

    /// <summary>
    /// The minimum number of characters read before parsing. Default is 4096.
    /// </summary>
    /// <remarks>
    /// When a parse depends on text that is not yet read, the buffered text grows by at least this amount,
    /// and at least doubles, before the parse is retried.
    /// Reads continue until that amount is available, so interactive sources should use a small value.
    /// </remarks>
    public int BufferSize { get; set; } = 4096;

    /// <summary>
    /// The maximum number of characters retained for the value being parsed. Default is <see cref="int.MaxValue"/>.
    /// </summary>
    /// <remarks>
    /// A value that needs more characters to be parsed fails with a <see cref="ParseException"/>.
    /// For <see cref="Parser{T}.ParseManyAsync(System.IO.TextReader, Func{T, CancellationToken, System.Threading.Tasks.ValueTask}, StreamParseOptions?, CancellationToken)"/>
    /// this limits the size of each item, otherwise it limits the size of the input.
    /// </remarks>
    public int MaxBufferedCharacters { get; set; } = int.MaxValue;

    /// <summary>
    /// The encoding used to decode a <see cref="System.IO.Stream"/>. Default is UTF-8.
    /// A byte order mark in the stream takes precedence.
    /// </summary>
    public Encoding? Encoding { get; set; }

    /// <summary>
    /// Whether <c>ParseManyAsync</c> skips white space, using <see cref="ParseContext.SkipWhiteSpace"/>, before each item,
    /// before each separator and before the end of the input. Default is <see langword="true"/>.
    /// </summary>
    public bool SkipWhiteSpace { get; set; } = true;

    /// <summary>
    /// Creates the <see cref="ParseContext"/> for a scanner, for instance to set <see cref="ParseContext.WhiteSpaceParser"/>
    /// or to use a custom context. The cancellation token must be passed to the context.
    /// Default creates a <see cref="ParseContext"/> with no other option.
    /// </summary>
    /// <remarks>
    /// A context is created each time more text is read, and <c>ParseManyAsync</c> shares it between the items read
    /// from the same text. Keep state that must outlive a context elsewhere.
    /// </remarks>
    public Func<Scanner, CancellationToken, ParseContext>? ContextFactory { get; set; }
}
