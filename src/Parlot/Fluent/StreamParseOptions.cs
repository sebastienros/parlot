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
    /// The minimum number of characters read before parsing, and each time more text is needed. Default is 4096.
    /// </summary>
    /// <remarks>
    /// When a single value is parsed, the compacting buffer reads at least as many characters as it retains.
    /// For <c>ParseManyAsync</c>, when a parse depends on text that is not yet read, the buffered text grows by at least
    /// this amount, and at least doubles, before the parse is retried.
    /// Reads continue until that amount is available, so interactive sources should use a small value.
    /// </remarks>
    public int BufferSize { get; set; } = 4096;

    /// <summary>
    /// The maximum number of characters buffered. Default is <see cref="int.MaxValue"/>.
    /// </summary>
    /// <remarks>
    /// When a single value is parsed, this limits the text the compacting buffer retains, which is the largest token
    /// or region that parsers can move back to. A parse that needs more fails with a <see cref="ParseException"/> at the start
    /// of the token that doesn't fit.
    /// For <see cref="Parser{T}.ParseManyAsync(System.IO.TextReader, Func{T, CancellationToken, System.Threading.Tasks.ValueTask}, StreamParseOptions?, CancellationToken)"/>
    /// this limits the size of each item, and a value that needs more characters fails with a <see cref="ParseException"/>.
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
    /// or to use a custom context. The context must use the scanner, and the cancellation token must be passed to it.
    /// Default creates a <see cref="ParseContext"/> with no other option.
    /// </summary>
    /// <remarks>
    /// A single value is parsed with one context. <c>ParseManyAsync</c> creates a context each time more text is read,
    /// and shares it between the items read from the same text. Keep state that must outlive a context elsewhere.
    /// </remarks>
    public Func<Scanner, CancellationToken, ParseContext>? ContextFactory { get; set; }
}
