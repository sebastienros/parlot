using System;
using System.Threading;

namespace Parlot.Fluent;

public abstract partial class Parser<T>
{
    /// <summary>
    /// Returns whether the parser matches the start of <paramref name="text"/>, without building its value.
    /// </summary>
    /// <param name="text">The text to match.</param>
    /// <returns><see langword="true"/> when <see cref="TryMatch(ParseContext, out TextSpan, out ParseError)"/> succeeds.</returns>
    public bool IsMatch(string text)
    {
        return TryMatch(new ParseContext(new Scanner(text)), out _, out _);
    }

    /// <summary>
    /// Returns whether the parser matches the start of <paramref name="text"/>, without building its value.
    /// </summary>
    /// <param name="text">The text to match.</param>
    /// <param name="cancellationToken">The token that cancels the match, which then fails.</param>
    /// <returns><see langword="true"/> when <see cref="TryMatch(ParseContext, out TextSpan, out ParseError)"/> succeeds.</returns>
    public bool IsMatch(string text, CancellationToken cancellationToken)
    {
        return TryMatch(new ParseContext(new Scanner(text), cancellationToken), out _, out _);
    }

    /// <summary>
    /// Returns whether the parser matches at the cursor of <paramref name="context"/>, without building its value.
    /// </summary>
    /// <param name="context">The context to match with, whose cursor is moved after the match.</param>
    /// <returns><see langword="true"/> when <see cref="TryMatch(ParseContext, out TextSpan, out ParseError)"/> succeeds.</returns>
    public bool IsMatch(ParseContext context)
    {
        return TryMatch(context, out _, out _);
    }

    /// <summary>
    /// Matches the start of <paramref name="text"/> and returns the text the parser matched, without building its value.
    /// </summary>
    /// <param name="text">The text to match.</param>
    /// <param name="match">The text the parser matched.</param>
    /// <returns>Whether the parser matched.</returns>
    /// <remarks>See <see cref="TryMatch(ParseContext, out TextSpan, out ParseError)"/>.</remarks>
    public bool TryMatch(string text, out TextSpan match)
    {
        return TryMatch(new ParseContext(new Scanner(text)), out match, out _);
    }

    /// <summary>
    /// Matches the start of <paramref name="text"/> and returns the text the parser matched, without building its value.
    /// </summary>
    /// <param name="text">The text to match.</param>
    /// <param name="cancellationToken">The token that cancels the match, which then fails.</param>
    /// <param name="match">The text the parser matched.</param>
    /// <returns>Whether the parser matched.</returns>
    /// <remarks>See <see cref="TryMatch(ParseContext, out TextSpan, out ParseError)"/>.</remarks>
    public bool TryMatch(string text, CancellationToken cancellationToken, out TextSpan match)
    {
        return TryMatch(new ParseContext(new Scanner(text), cancellationToken), out match, out _);
    }

    /// <summary>
    /// Matches the start of <paramref name="text"/> and returns the text the parser matched, without building its value.
    /// </summary>
    /// <param name="text">The text to match.</param>
    /// <param name="match">The text the parser matched.</param>
    /// <param name="error">The error that failed the match, when one did.</param>
    /// <returns>Whether the parser matched.</returns>
    /// <remarks>See <see cref="TryMatch(ParseContext, out TextSpan, out ParseError)"/>.</remarks>
    public bool TryMatch(string text, out TextSpan match, out ParseError? error)
    {
        return TryMatch(new ParseContext(new Scanner(text)), out match, out error);
    }

    /// <summary>
    /// Matches the start of <paramref name="text"/> and returns the text the parser matched, without building its value.
    /// </summary>
    /// <param name="text">The text to match.</param>
    /// <param name="cancellationToken">The token that cancels the match, which then fails.</param>
    /// <param name="match">The text the parser matched.</param>
    /// <param name="error">The error that failed the match, when one did.</param>
    /// <returns>Whether the parser matched.</returns>
    /// <remarks>See <see cref="TryMatch(ParseContext, out TextSpan, out ParseError)"/>.</remarks>
    public bool TryMatch(string text, CancellationToken cancellationToken, out TextSpan match, out ParseError? error)
    {
        return TryMatch(new ParseContext(new Scanner(text), cancellationToken), out match, out error);
    }

    /// <summary>
    /// Matches at the cursor of <paramref name="context"/> and returns the text the parser matched, without building its
    /// value.
    /// </summary>
    /// <param name="context">The context to match with, whose cursor is moved after the match.</param>
    /// <param name="match">The text the parser matched.</param>
    /// <param name="error">The error that failed the match, when one did.</param>
    /// <returns>Whether the parser matched.</returns>
    /// <remarks>
    /// <para>
    /// It matches what <see cref="TryParse(ParseContext, out T, out ParseError)"/> parses, the parser doesn't have to reach
    /// the end of the text, unless it ends with <c>Eof()</c>. The lists, tuples and options of the parsers are not built,
    /// the callbacks of <c>Then</c>, <c>When</c> and the other parsers which read a value are still invoked with it.
    /// </para>
    /// <para>
    /// The text is the one <see cref="Parsers.Capture{T}(Parser{T})"/> returns, from the cursor to where the parser ended:
    /// it includes the white space that a <c>Terms</c> parser skips before its first token. A parse error, like the ones
    /// of <c>ElseError</c>, or a cancellation fails the match with <paramref name="error"/>, as with <c>TryParse</c>.
    /// </para>
    /// </remarks>
    public bool TryMatch(ParseContext context, out TextSpan match, out ParseError? error)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        error = null;

        try
        {
            var start = context.Scanner.Cursor.Offset;
            var result = new ParseResult<T>();

            // The matched text must still be buffered once the parser is done
            var pin = context.Pin();
            var success = ParseWithoutValue(context, ref result);
            context.Unpin(pin);

            if (success)
            {
                var cursor = context.Scanner.Cursor;
                match = cursor.CreateSpan(start, cursor.Offset - start);
                return true;
            }
        }
        catch (ParseException e)
        {
            error = new ParseError
            {
                Message = e.Message,
                Position = e.Position
            };
        }
        catch (OperationCanceledException e)
        {
            error = new ParseError
            {
                Message = e.Message,
                Position = context.Scanner.Cursor.Position
            };
        }

        match = default;
        return false;
    }
}
