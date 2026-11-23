using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Parlot.Fluent;

public abstract partial class Parser<T> : IParserText
{
    /// <summary>
    /// Gets or sets the text which is to render the textual representation of the parser.
    /// </summary>
    public string? Name { get; set; }

    public T? Parse(string text)
    {
        var context = new ParseContext(new Scanner(text));

        return Parse(context);
    }

    public T? Parse(string text, CancellationToken cancellationToken)
    {
        var context = new ParseContext(new Scanner(text), cancellationToken);

        return Parse(context);
    }

    public T? Parse(ParseContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        context.Scanner.Cursor.ResetFurthestOffset();

        var localResult = new ParseResult<T>();

        var success = Parse(context, ref localResult);

        if (success)
        {
            return localResult.Value;
        }

        return default;
    }

    public bool TryParse(string text, out T? value)
    {
        // The error isn't returned: the one of a reason isn't built
        return TryParse(new ParseContext(new Scanner(text)), out value!, out _, withReason: false);
    }

    public bool TryParse(string text, CancellationToken cancellationToken, out T? value)
    {
        return TryParse(new ParseContext(new Scanner(text), cancellationToken), out value!, out _, withReason: false);
    }

    /// <summary>
    /// Tries to parse at the cursor of <paramref name="context"/> without constructing a <see cref="ParseError"/>.
    /// </summary>
    /// <param name="context">The context to parse with.</param>
    /// <param name="value">The parsed value when parsing succeeds; otherwise <see langword="default"/>.</param>
    /// <returns><see langword="true"/> when parsing succeeds; otherwise <see langword="false"/>.</returns>
    public bool TryParse(ParseContext context, out T? value)
    {
        return TryParse(context, out value!, out _, withReason: false);
    }

    public bool TryParse(string text, [MaybeNullWhen(false)] out T value, out ParseError? error)
    {
        return TryParse(new ParseContext(new Scanner(text)), out value, out error);
    }

    public bool TryParse(string text, CancellationToken cancellationToken, [MaybeNullWhen(false)] out T value, out ParseError? error)
    {
        return TryParse(new ParseContext(new Scanner(text), cancellationToken), out value, out error);
    }

    public bool TryParse(ParseContext context, [MaybeNullWhen(false)] out T value, out ParseError? error)
    {
        return TryParse(context, out value, out error, withReason: true);
    }

    // withReason: whether a failure with a reason returns its error, which computes its position
    private bool TryParse(ParseContext context, [MaybeNullWhen(false)] out T value, out ParseError? error, bool withReason)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        error = null;

        context.Scanner.Cursor.ResetFurthestOffset();

        try
        {
            var localResult = new ParseResult<T>();

            var success = Parse(context, ref localResult);

            if (success)
            {
                value = localResult.Value;
                return true;
            }

            if (withReason)
            {
                error = ParseError.FromFurthestReason(context);
            }
        }
        catch (ParseException e)
        {
            error = new ParseError
            {
                Message = e.Message,
                Position = e.Position,
                IsNestedTooDeeply = e.IsNestedTooDeeply
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

        value = default!;
        return false;
    }

    public override string ToString() => $"{Name ?? GetType().Name}";

    /// <summary>
    /// Formats the text of this parser for <see cref="ToString"/>, once per call to <see cref="ToString"/>: where the
    /// parser is reached again, from another parser which shares it or through a cycle, it is written as a reference,
    /// its <see cref="Name"/> or <c>#n</c>, and its text is labelled with it, <c>#n:(...)</c>. The parsers which the
    /// text interpolates write their own text in the same builder.
    /// </summary>
#pragma warning disable CA1822 // The handler receives this parser
    private protected string FormatOnce([InterpolatedStringHandlerArgument("")] ref ParserTextHandler handler)
        => handler.ToStringAndClear();
#pragma warning restore CA1822

    /// <summary>
    /// Whether this parser only adds to the text of the parser it invokes, and is written each time it is reached.
    /// </summary>
    private protected virtual bool IsTextWrapper => false;

    bool IParserText.IsTextWrapper => IsTextWrapper;
}
