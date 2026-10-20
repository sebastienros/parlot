using Parlot.Rewriting;
using System;

namespace Parlot.Fluent;

/// <summary>
/// Matches an operator parser and returns its index in the list of operators, so a <see cref="OneOf{T}"/>
/// can select the operator and the factory to apply with it.
/// </summary>
/// <typeparam name="T">The type of the operator parser.</typeparam>
internal sealed class OperatorIndex<T> : Parser<int>, ISeekable
{
    private readonly Parser<T> _parser;
    private readonly int _index;

    public OperatorIndex(Parser<T> parser, int index)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _index = index;

        if (_parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<int> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_parser.Parse(context, ref parsed))
        {
            result.Set(parsed.Start, parsed.End, _index);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public override string ToString() => _parser.ToString() ?? "";
}
