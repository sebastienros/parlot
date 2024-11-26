using Parlot.Compilation;
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class WhiteSpaceLiteral : Parser<TextSpan>, ICompilable
{
    private readonly bool _includeNewLines;

    public WhiteSpaceLiteral(bool includeNewLines)
    {
        _includeNewLines = includeNewLines;

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
