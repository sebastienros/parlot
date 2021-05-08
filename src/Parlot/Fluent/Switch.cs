using Parlot.Compilation;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{

    /// <summary>
    /// Routes the parsing based on a custom delegate.
    /// </summary>
    public sealed class Switch<T, U, TParseContext, TChar> : Parser<U, TParseContext, TChar>, ICompilable<TParseContext, TChar>
    where TParseContext : ParseContextWithScanner<Scanner<TChar>, TChar>
    where TChar : IEquatable<TChar>, IConvertible
    {
        private readonly Parser<T, TParseContext> _previousParser;
        private readonly Func<TParseContext, T, Parser<U, TParseContext>> _action;
        public Switch(Parser<T, TParseContext> previousParser, Func<TParseContext, T, Parser<U, TParseContext>> action)
        {
            _previousParser = previousParser ?? throw new ArgumentNullException(nameof(previousParser));
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override bool Parse(TParseContext context, ref ParseResult<U> result)
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

        public CompilationResult Compile(CompilationContext<TParseContext, TChar> context)
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
            var nextParser = Expression.Parameter(typeof(Parser<U, TParseContext, TChar>));
            var parseResult = Expression.Variable(typeof(ParseResult<U>), $"value{context.NextNumber}");

            var block = Expression.Block(
                    previousParserCompileResult.Variables,
                    previousParserCompileResult.Body
                    .Append(
                        Expression.IfThen(
                            previousParserCompileResult.Success,
                            Expression.Block(
                                new[] { nextParser, parseResult },
                                Expression.Assign(nextParser, Expression.Invoke(Expression.Constant(_action), new[] { context.ParseContext, previousParserCompileResult.Value })),
                                Expression.IfThen(
                                    Expression.NotEqual(Expression.Constant(null), nextParser),
                                    Expression.Block(
                                        Expression.Assign(success,
                                            Expression.Call(
                                                nextParser,
                                                typeof(Parser<U, TParseContext>).GetMethod("Parse", new[] { typeof(TParseContext), typeof(ParseResult<U>).MakeByRefType() }),
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



    /// <summary>
    /// Routes the parsing based on a custom delegate.
    /// </summary>
    public sealed class Switch<T, U, TParseContext> : Parser<U, TParseContext>, ICompilable<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Parser<T, TParseContext> _previousParser;
        private readonly Func<TParseContext, T, Parser<U, TParseContext>> _action;
        public Switch(Parser<T, TParseContext> previousParser, Func<TParseContext, T, Parser<U, TParseContext>> action)
        {
            _previousParser = previousParser ?? throw new ArgumentNullException(nameof(previousParser));
            _action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override bool Parse(TParseContext context, ref ParseResult<U> result)
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

        public CompilationResult Compile(CompilationContext<TParseContext> context)
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
            var nextParser = Expression.Parameter(typeof(Parser<U, TParseContext>));
            var parseResult = Expression.Variable(typeof(ParseResult<U>), $"value{context.NextNumber}");

            var block = Expression.Block(
                    previousParserCompileResult.Variables,
                    previousParserCompileResult.Body
                    .Append(
                        Expression.IfThen(
                            previousParserCompileResult.Success,
                            Expression.Block(
                                new[] { nextParser, parseResult },
                                Expression.Assign(nextParser, Expression.Invoke(Expression.Constant(_action), new[] { context.ParseContext, previousParserCompileResult.Value })),
                                Expression.IfThen(
                                    Expression.NotEqual(Expression.Constant(null), nextParser),
                                    Expression.Block(
                                        Expression.Assign(success,
                                            Expression.Call(
                                                nextParser,
                                                typeof(Parser<U, TParseContext>).GetMethod("Parse", new[] { typeof(TParseContext), typeof(ParseResult<U>).MakeByRefType() }),
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
