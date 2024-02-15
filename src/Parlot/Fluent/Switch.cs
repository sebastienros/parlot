using Parlot.Compilation;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{

    /// <summary>
    /// Routes the parsing based on a custom delegate.
    /// </summary>
    public sealed class Switch<T, U> : Parser<U>, ICompilable
    {

        private readonly Parser<T> _previousParser;
        private readonly Func<ParseContext, T, Parser<U>> _action;
        public Switch(Parser<T> previousParser, Func<ParseContext, T, Parser<U>> action)
        {
            _previousParser = previousParser ?? throw new ArgumentNullException(nameof(previousParser));
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override bool Parse(ParseContext context, ref ParseResult<U> result)
        {
            var previousResult = new ParseResult<T>();

            if (!_previousParser.Parse(context, ref previousResult))
            {
                return false;
            }

            var nextParser = _action(context, previousResult.Value);

            if (nextParser == null)
            {
                return false;
            }

            var parsed = new ParseResult<U>();

            if (nextParser.Parse(context, ref parsed))
            {
                result.Set(parsed.Start, parsed.End, parsed.Value);
                return true;
            }

            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(U)));

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
                                    Expression.NotEqual(Expression.Constant(null), nextParser),
                                    Expression.Block(
                                        Expression.Assign(success,
                                            Expression.Call(
                                                nextParser,
                                                typeof(Parser<U>).GetMethod("Parse", [typeof(ParseContext), typeof(ParseResult<U>).MakeByRefType()]),
                                                context.ParseContext,
                                                parseResult)),
                                        context.DiscardResult
                                            ? Expression.Empty()
                                            : Expression.IfThen(success, Expression.Assign(value, Expression.Field(parseResult, "Value")))
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
}
