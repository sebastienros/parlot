using Parlot.Compilation;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// A parser that succeeds when parsing whitespaces as defined in <see cref="ParseContext.WhiteSpaceParser"/>.
/// </summary>
public sealed class WhiteSpaceParser : Parser<TextSpan>, ICompilable
{
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

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<TextSpan>();

        var start = context.DeclareOffsetVariable(result);

        result.Body.Add(context.ParserSkipWhiteSpace());

        var end = context.DeclareOffsetVariable(result);

        result.Body.Add(
            Expression.IfThen(
                Expression.NotEqual(start, end),
                Expression.Block(
                    Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                    context.DiscardResult ? Expression.Empty() : Expression.Assign(result.Value, context.NewTextSpan(context.Buffer(), start, Expression.Subtract(end, start)))
                )
            )
        );

        return result;
    }

    public override string ToString() => "WhiteSpaceParser";
}
