using Parlot.Compilation;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;


/// <summary>
/// Routes the parsing based on a custom delegate.
/// </summary>
public sealed class Switch<T, U> : Parser<U>, ICompilable
{
    private static readonly MethodInfo _uParse = typeof(Parser<U>).GetMethod("Parse", [typeof(ParseContext), typeof(ParseResult<U>).MakeByRefType()])!;

    private readonly Parser<T> _previousParser;
    private readonly Func<ParseContext, T, Parser<U>> _action;
    public Switch(Parser<T> previousParser, Func<ParseContext, T, Parser<U>> action)
    {
        _previousParser = previousParser ?? throw new ArgumentNullException(nameof(previousParser));
        _action = action ?? throw new ArgumentNullException(nameof(action));

        Name = $"{previousParser.Name} (Switch)";
    }

    public override bool Parse(ParseContext context, ref ParseResult<U> result)
    {
        context.EnterParser(this);

        var previousResult = new ParseResult<T>();

        if (!_previousParser.Parse(context, ref previousResult))
        {
            context.ExitParser(this);
            return false;
        }

        var nextParser = _action(context, previousResult.Value);

        if (nextParser == null)
        {
            context.ExitParser(this);
            return false;
        }

        var parsed = new ParseResult<U>();

        if (nextParser.Parse(context, ref parsed))
        {
            result.Set(parsed.Start, parsed.End, parsed.Value);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<U>();

        // previousParser instructions
        // 
        // if (previousParser.Success)
        // {
        //    var nextParser = _action(context, previousParser.Value);
        //
        //    if (nextParser != null)
        //    {
        //       var parsed = new ParseResult<U>();
        //
        //       if (nextParser.Parse(context, ref parsed))
        //       {
        //           value = parsed.Value;
        //           success = true;
        //       }
        //    }
        // }

        var previousParserCompileResult = _previousParser.Build(context, requireResult: true);
        var nextParser = Expression.Parameter(typeof(Parser<U>));
        var parseResult = Expression.Variable(typeof(ParseResult<U>), $"value{context.NextNumber}");

        var block = Expression.Block(
                previousParserCompileResult.Variables,
                previousParserCompileResult.Body
                .Append(
                    Expression.IfThen(
                        previousParserCompileResult.Success,
                        Expression.Block(
                            [nextParser, parseResult],
                            Expression.Assign(nextParser, Expression.Invoke(Expression.Constant(_action), new[] { context.ParseContext, previousParserCompileResult.Value })),
                            Expression.IfThen(
                                Expression.NotEqual(Expression.Constant(null, typeof(Parser<U>)), nextParser),
                                Expression.Block(
                                    Expression.Assign(result.Success,
                                        Expression.Call(
                                            nextParser,
                                            _uParse,
                                            context.ParseContext,
                                            parseResult)),
                                    context.DiscardResult
                                        ? Expression.Empty()
                                        : Expression.IfThen(result.Success, Expression.Assign(result.Value, Expression.Field(parseResult, "Value")))
                                    )
                                )
                            )
                        )
                    )
                );

        result.Body.Add(block);

        return result;
    }
}
