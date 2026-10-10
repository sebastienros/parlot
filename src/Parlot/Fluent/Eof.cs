using Parlot.Compilation;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Successful when the cursor is at the end of the string.
/// </summary>
public sealed class Eof<T> : Parser<T>, ICompilable
{
    private readonly Parser<T> _parser;

    public Eof(Parser<T> parser)
    {
        _parser = parser;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Position;

        if (_parser.Parse(context, ref result))
        {
            if (cursor.Eof)
            {
                context.ExitParser(this);
                return true;
            }

            cursor.ResetPosition(start);
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        // parse1 instructions
        // 
        // if (parser1.Success && context.Scanner.Cursor.Eof)
        // {
        //    value = parse1.Value;
        //    success = true;
        // }

        var start = context.DeclarePositionVariable(result);
        var parserCompileResult = _parser.Build(context);

        result.Body.Add(
            Expression.Block(
                parserCompileResult.Variables,
                Expression.Block(parserCompileResult.Body),
                Expression.IfThenElse(
                    Expression.AndAlso(parserCompileResult.Success, context.Eof()),
                    Expression.Block(
                        context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Assign(result.Value, parserCompileResult.Value),
                        Expression.Assign(result.Success, Expression.Constant(true, typeof(bool)))
                        ),
                    context.ResetPosition(start)
                    )
                )
            );

        return result;
    }

    public override string ToString() => $"{_parser} (Eof)";
}
