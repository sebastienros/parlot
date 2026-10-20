using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Returns a default value if the previous parser failed.
/// </summary>
public sealed class Else<T> : Parser<T>, ICompilable
{
    private readonly Parser<T> _parser;
    private readonly T? _value;
    private readonly Func<ParseContext, T>? _func;

    public Else(Parser<T> parser, T value)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _value = value;
    }

    public Else(Parser<T> parser, Func<ParseContext, T> func)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _func = func ?? throw new ArgumentNullException(nameof(func));
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        if (!_parser.Parse(context, ref result))
        {
            // The parser reset the cursor, and may have left the positions of a partial match in the result
            var offset = context.Scanner.Cursor.Offset;

            if (_func != null)
            {
                result.Set(offset, offset, _func(context));
            }
            else
            {
                // _value can't be null if _func is null
                result.Set(offset, offset, _value!);
            }
        }

        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>(true);

        var parserCompileResult = _parser.Build(context);

        // success = true;
        //
        // parser instructions
        // 
        // if (parser.success)
        // {
        //    value = parser.Value
        // }
        // else
        // {
        //   value = _func != null ? _func(context) : _value
        // }

        Expression elseExpression;
        if (_func != null)
        {
            var invokeFuncExpression = Expression.Invoke(Expression.Constant(_func), [context.ParseContext]);
            elseExpression = context.DiscardResult
                ? invokeFuncExpression
                : Expression.Assign(result.Value, invokeFuncExpression);
        }
        else
        {
            elseExpression = context.DiscardResult
                ? Expression.Empty()
                : Expression.Assign(result.Value, Expression.Constant(_value, typeof(T)));
        }

        result.Body.Add(
            Expression.Block(
                parserCompileResult.Variables,
                Expression.Block(parserCompileResult.Body),
                context.DiscardResult
                ? Expression.Empty()
                : Expression.IfThenElse(
                    parserCompileResult.Success,
                    Expression.Assign(result.Value, parserCompileResult.Value),
                    elseExpression
                )
            )
        );

        return result;
    }

    public override string ToString() => $"{_parser} (Else)";
}
