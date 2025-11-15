using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// A parser that succeeds when parsing whitespaces as defined in <see cref="ParseContext.WhiteSpaceParser"/>.
/// </summary>
public sealed class WhiteSpaceParser : Parser<TextSpan>
{
    public WhiteSpaceParser()
    {
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var start = context.Scanner.Cursor.Offset;

        context.SkipWhiteSpace();

        var end = context.Scanner.Cursor.Offset;

        if (start == end)
        {
            context.ExitParser(this);
            return false;
        }

        result.Set(start, end, new TextSpan(context.Scanner.Buffer, start, end - start));

        context.ExitParser(this);
        return true;
    }

    public override string ToString() => $"WhiteSpaceParser";
}
