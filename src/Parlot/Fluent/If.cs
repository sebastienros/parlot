using Parlot.Compilation;
using Parlot.SourceGeneration;
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
public sealed class If<C, S, T> : Parser<T>, ICompilable, ISourceable where C : ParseContext
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
//                                    parserCompileResult.Success

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

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("If requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T));
        var cursorName = context.CursorName;
        var ctx = context.ParseContextName;

        var startName = $"start{context.NextNumber()}";
        result.Body.Add($"var {startName} = {cursorName}.Position;");

        // Register the predicate lambda
        var predicateLambda = context.RegisterLambda(_predicate);
        var stateLambda = context.RegisterLambda(new Func<S?>(() => _state));
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Use helper instead of inlining
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_If", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        // if (_predicate((C)context, _state))
        // {
        //     if (Helper(context, out value))
        //     {
        //         success = true;
        //     }
        // }
        // if (!success)
        // {
        //     cursor.ResetPosition(start);
        // }

        result.Body.Add($"if ({predicateLambda}(({SourceGenerationContext.GetTypeName(typeof(C))}){ctx}, {stateLambda}()))");
        result.Body.Add("{");
        result.Body.Add($"    if ({helperName}({ctx}, out {result.ValueVariable}))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add($"if (!{result.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (If)";
}
