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
            if (_func != null)
            {
                result.Set(result.Start, result.End, _func(context));
            }
            else
            {
                result.Set(result.Start, result.End, _value!);
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
            elseExpression = context.DiscardResult
                ? Expression.Invoke(Expression.Constant(_func), [context.ParseContext])
                : Expression.Assign(result.Value, Expression.Invoke(Expression.Constant(_func), [context.ParseContext]));
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
