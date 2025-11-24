using Parlot.Compilation;
using Parlot.Rewriting;
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class WhiteSpaceLiteral : Parser<TextSpan>, ICompilable, ISeekable
{
    private readonly bool _includeNewLines;

    private static char[] _whiteSpaceChars =>
    [
        '\u0009', '\u000C', '\u0020', '\u00A0', '\u1680', '\u180E', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200A', '\u202F', '\u205F', '\u3000', '\uFEFF'
    ];

    private static char[] _whiteSpaceOrNewLineChars =>
    [
        '\u0009', '\u000A', '\u000B', '\u000C', '\u000D', '\u0020', '\u00A0', '\u1680', '\u180E', '\u2000', '\u2001', '\u2002', '\u2003', '\u2004', '\u2005', '\u2006', '\u2007', '\u2008', '\u2009', '\u200A', '\u202F', '\u205F', '\u3000', '\uFEFF'
    ];

    public bool CanSeek { get; } = true;

    public char[] ExpectedChars {get; private set;}

    public bool SkipWhitespace { get; }

    public WhiteSpaceLiteral(bool includeNewLines)
    {
        _includeNewLines = includeNewLines;

        ExpectedChars = includeNewLines ? _whiteSpaceOrNewLineChars : _whiteSpaceChars;

        Name = "WhiteSpaceLiteral";
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var start = context.Scanner.Cursor.Offset;

        if (_includeNewLines)
        {
            context.Scanner.SkipWhiteSpaceOrNewLine();
        }
        else
        {
            context.Scanner.SkipWhiteSpace();
        }

        var end = context.Scanner.Cursor.Offset;

        if (start == end)
        {
            context.ExitParser(this);
            return false;
        }

        result.Set(start, context.Scanner.Cursor.Offset, new TextSpan(context.Scanner.Buffer, start, end - start));

        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<TextSpan>();

        var start = context.DeclareOffsetVariable(result);

        result.Body.Add(
            _includeNewLines
                ? context.SkipWhiteSpaceOrNewLine()
                : context.SkipWhiteSpace()
            );

        var end = context.DeclareOffsetVariable(result);

        result.Body.Add(
            Expression.Block(
                Expression.IfThen(
                    Expression.NotEqual(start, end),
                    Expression.Assign(result.Success, Expression.Constant(true, typeof(bool)))
                    ),
                context.DiscardResult ? Expression.Empty() : Expression.Assign(result.Value, context.NewTextSpan(context.Buffer(), start, Expression.Subtract(end, start)))
                )
            );

        return result;
    }
}
