using System;
using System.Threading;

namespace Parlot.Fluent;

public abstract partial class Parser<T>
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

    public bool TryParse(string text, out T value, out ParseError? error)
    {
        return TryParse(new ParseContext(new Scanner(text)), out value, out error);
    }

    public bool TryParse(string text, CancellationToken cancellationToken, out T value, out ParseError? error)
    {
        return TryParse(new ParseContext(new Scanner(text), cancellationToken), out value, out error);
    }

    public bool TryParse(ParseContext context, out T value, out ParseError? error)
    {
        return TryParse(context, out value, out error, withReason: true);
    }

    // withReason: whether a failure with a reason returns its error, which computes its position
    private bool TryParse(ParseContext context, out T value, out ParseError? error, bool withReason)
    {
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
                error = ParseError.FromFurthestReason(context.Scanner.Cursor);
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
}
