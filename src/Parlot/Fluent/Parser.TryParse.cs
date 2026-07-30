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
        return TryParse(text, out value, out _);
    }

    public bool TryParse(string text, CancellationToken cancellationToken, out T? value)
    {
        return TryParse(text, cancellationToken, out value, out _);
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
        error = null;

        try
        {
            var localResult = new ParseResult<T>();

            var success = Parse(context, ref localResult);

            if (success)
            {
                value = localResult.Value;
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

        value = default!;
        return false;
    }

    public override string ToString() => $"{Name ?? GetType().Name}";
}
