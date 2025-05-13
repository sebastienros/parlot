using Parlot.Compilation;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Returns a list containing zero or one element.
/// </summary>
/// <remarks>
/// This parser will always succeed. If the previous parser fails, it will return an empty list.
/// </remarks>
public sealed class Optional<T> : Parser<IReadOnlyList<T>>, ICompilable
{
    private readonly Parser<T> _parser;
    public Optional(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public override bool Parse(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        var success = _parser.Parse(context, ref parsed);

        result.Set(parsed.Start, parsed.End, success ? [parsed.Value] : []);

        // Optional always succeeds
        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<IReadOnlyList<T>>(true, ExpressionHelper.ArrayEmpty<T>());

        // T value = _defaultValue;
        //
        // parse1 instructions
        // 
        // value = new OptionalResult<T>(parser1.Success, success ? [parsed.Value] : []);
        //

        var parserCompileResult = _parser.Build(context);

        var block = Expression.Block(
            parserCompileResult.Variables,
                Expression.Block(
                    Expression.Block(parserCompileResult.Body),
                    context.DiscardResult
                        ? Expression.Empty()
                        : Expression.IfThenElse(
                            parserCompileResult.Success,
                            Expression.Assign(result.Value, Expression.NewArrayInit(typeof(T), parserCompileResult.Value)),
                            Expression.Assign(result.Value, Expression.Constant(Array.Empty<T>(), typeof(T[])))
                        )
                    )
                );

        result.Body.Add(block);

        return result;
    }

    public override string ToString() => $"{_parser}?";
}
