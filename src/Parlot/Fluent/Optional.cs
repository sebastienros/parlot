using Parlot.Compilation;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

/// <summary>
/// Returns a list containing zero or one element.
/// </summary>
/// <remarks>
/// This parser will always succeed. If the previous parser fails, it will return an empty list.
/// </remarks>
public sealed class Optional<T> : Parser<Option<T>>, ICompilable
{
    private static readonly ConstructorInfo _optionConstructor = typeof(Option<T>).GetConstructor([typeof(T)])!;
    
    private readonly Parser<T> _parser;
    public Optional(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public override bool Parse(ParseContext context, ref ParseResult<Option<T>> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        var success = _parser.Parse(context, ref parsed);

        result.Set(parsed.Start, parsed.End, success ? new Option<T>(parsed.Value) : new Option<T>());

        // Optional always succeeds
        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<Option<T>>(true);

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
                            Expression.Assign(result.Value, Expression.New(_optionConstructor, parserCompileResult.Value)),
                            Expression.Assign(result.Value, Expression.Constant(new Option<T>(), typeof(Option<T>)))
                        )
                    )
                );

        result.Body.Add(block);

        return result;
    }

    public override string ToString() => $"{_parser}?";
}
