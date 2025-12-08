using Parlot.Compilation;
using Parlot.SourceGeneration;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Returns a default value if the previous parser failed.
/// </summary>
public sealed class Else<T> : Parser<T>, ICompilable, ISourceable
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
                // _value can't be null if _func is null
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

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Else requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T), defaultSuccess: true);

        var inner = sourceable.GenerateSource(context);

        // Emit inner parser locals and body
        foreach (var local in inner.Locals)
        {
            result.Body.Add(local);
        }

        foreach (var stmt in inner.Body)
        {
            result.Body.Add(stmt);
        }

        // if (inner.success)
        // {
        //     value = inner.value;
        // }
        // else
        // {
        //     value = _func != null ? _func(context) : _value;
        // }
        // success = true; (always succeeds)
        
        result.Body.Add($"if ({inner.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {result.ValueVariable} = {inner.ValueVariable};");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        
        if (_func != null)
        {
            var lambdaId = context.RegisterLambda(_func);
            result.Body.Add($"    {result.ValueVariable} = {lambdaId}({context.ParseContextName});");
        }
        else
        {
            var lambdaId = context.RegisterLambda(new Func<T>(() => _value!));
            result.Body.Add($"    {result.ValueVariable} = {lambdaId}();");
        }
        
        result.Body.Add("}");
        result.Body.Add($"{result.SuccessVariable} = true;");

        return result;
    }

    public override string ToString() => $"{_parser} (Else)";
}
