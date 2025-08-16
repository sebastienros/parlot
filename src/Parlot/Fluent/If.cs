using Parlot.Compilation;
using System;
#if NET
using System.Linq;
#endif
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Ensure the given parser is valid based on a condition, and backtracks if not.
/// </summary>
/// <typeparam name="C">The concrete <see cref="ParseContext" /> type to use.</typeparam>
/// <typeparam name="S">The type of the state to pass.</typeparam>
/// <typeparam name="T">The output parser type.</typeparam>
public sealed class If<C, S, T> : Parser<T>, ICompilable where C : ParseContext
{
    private readonly Func<C, S?, bool> _predicate;
    private readonly S? _state;
    private readonly Parser<T> _parser;

    public If(Parser<T> parser, Func<C, S?, bool> predicate, S? state)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _state = state;
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var valid = _predicate((C)context, _state);

        if (valid)
        {
            var start = context.Scanner.Cursor.Position;

            if (!_parser.Parse(context, ref result))
            {
                context.Scanner.Cursor.ResetPosition(start);
            }
        }

        context.ExitParser(this);
        return valid;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        var parserCompileResult = _parser.Build(context, requireResult: true);

        // success = false;
        // value = default;
        //
        // start = context.Scanner.Cursor.Position;
        // if (_predicate((C)context, _state) )
        // {
        //   parser instructions
        //
        //   if (parser.success)
        //   {
        //     success = true;
        //     value = parser.Value;
        //   }
        // }
        // 
        // if (!success)
        // {
        //    context.ResetPosition(start);
        // }
        //

        var start = context.DeclarePositionVariable(result);

        var block = Expression.Block(
                Expression.IfThen(
                    Expression.Invoke(Expression.Constant(_predicate), [Expression.Convert(context.ParseContext, typeof(C)), Expression.Constant(_state, typeof(S))]),
                    Expression.Block(
                        Expression.Block(
                            parserCompileResult.Variables,
                            parserCompileResult.Body),
                        Expression.IfThen(
                            parserCompileResult.Success,
                            Expression.Block(
                                Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                                    context.DiscardResult
                                        ? Expression.Empty()
                                        : Expression.Assign(result.Value, parserCompileResult.Value)
                            )
                        )
                    )
                ),
                Expression.IfThen(
                    Expression.Not(result.Success),
                    context.ResetPosition(start)
                    )
                );


        result.Body.Add(block);

        return result;
    }

    public override string ToString() => $"{_parser} (If)";
}
