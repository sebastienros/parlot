using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class Between<A, T, B, TParseContext> : Parser<T, TParseContext>, ICompilable<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Parser<T, TParseContext> _parser;
        private readonly Parser<A, TParseContext> _before;
        private readonly Parser<B, TParseContext> _after;

        public Between(Parser<A, TParseContext> before, Parser<T, TParseContext> parser, Parser<B, TParseContext> after)
        {
            _before = before ?? throw new ArgumentNullException(nameof(before));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _after = after ?? throw new ArgumentNullException(nameof(after));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var parsedA = new ParseResult<A>();

            if (!_before.Parse(context, ref parsedA))
            {
                return false;
            }

            if (!_parser.Parse(context, ref result))
            {
                return false;
            }

            var parsedB = new ParseResult<B>();

            if (!_after.Parse(context, ref parsedB))
            {
                return false;
            }

            return true;
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T)));

            // before instructions
            // 
            // if (before.Success)
            // {
            //    parser instructions
            //    
            //    if (parser.Success)
            //    {
            //       after instructions
            //    
            //       if (after.Success)
            //       {
            //          success = true;
            //          value = parser.Value;
            //       }  
            //    }
            // }

            var beforeCompileResult = _before.Build(context);
            var parserCompileResult = _parser.Build(context);
            var afterCompileResult = _after.Build(context);

            var block = Expression.Block(
                    beforeCompileResult.Variables,
                    Expression.Block(beforeCompileResult.Body),
                    Expression.IfThen(
                        beforeCompileResult.Success,
                        Expression.Block(
                            parserCompileResult.Variables,
                            Expression.Block(parserCompileResult.Body),
                            Expression.IfThen(
                                parserCompileResult.Success,
                                Expression.Block(
                                    afterCompileResult.Variables,
                                    Expression.Block(afterCompileResult.Body),
                                    Expression.IfThen(
                                        afterCompileResult.Success,
                                        Expression.Block(
                                            Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                                            context.DiscardResult
                                            ? Expression.Empty()
                                            : Expression.Assign(value, parserCompileResult.Value)
                                            )
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


    public sealed class Between<A, T, B, TParseContext, TChar> : Parser<T, TParseContext, TChar>, ICompilable<TParseContext, TChar>
    where TParseContext : ParseContextWithScanner<Scanner<TChar>, TChar>
    where TChar : IEquatable<TChar>, IConvertible
    {
        private readonly Parser<T, TParseContext> _parser;
        private readonly Parser<A, TParseContext> _before;
        private readonly Parser<B, TParseContext> _after;

        public Between(Parser<A, TParseContext> before, Parser<T, TParseContext> parser, Parser<B, TParseContext> after)
        {
            _before = before ?? throw new ArgumentNullException(nameof(before));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _after = after ?? throw new ArgumentNullException(nameof(after));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            var parsedA = new ParseResult<A>();

            if (!_before.Parse(context, ref parsedA))
            {
                // Don't reset position since _before should do it
                return false;
            }

            if (!_parser.Parse(context, ref result))
            {
                context.Scanner.Cursor.ResetPosition(start);
                return false;
            }

            var parsedB = new ParseResult<B>();

            if (!_after.Parse(context, ref parsedB))
            {
                context.Scanner.Cursor.ResetPosition(start);
                return false;
            }

            return true;
        }

        public CompilationResult Compile(CompilationContext<TParseContext, TChar> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T)));

            // start = context.Scanner.Cursor.Position;
            //
            // before instructions
            //
            // if (before.Success)
            // {
            //      parser instructions
            //      
            //      if (parser.Success)
            //      {
            //         after instructions
            //      
            //         if (after.Success)
            //         {
            //            success = true;
            //            value = parser.Value;
            //         }  
            //      }
            //
            //      if (!success)
            //      {  
            //          resetPosition(start);
            //      }
            // }

            var beforeCompileResult = _before.Build(context);
            var parserCompileResult = _parser.Build(context);
            var afterCompileResult = _after.Build(context);

            var start = context.DeclarePositionVariable(result);

            var block = Expression.Block(
                    beforeCompileResult.Variables,
                    Expression.Block(beforeCompileResult.Body),
                    Expression.IfThen(
                        beforeCompileResult.Success,
                        Expression.Block(
                            parserCompileResult.Variables,
                            Expression.Block(parserCompileResult.Body),
                            Expression.IfThen(
                                parserCompileResult.Success,
                                Expression.Block(
                                    afterCompileResult.Variables,
                                    Expression.Block(afterCompileResult.Body),
                                    Expression.IfThen(
                                        afterCompileResult.Success,
                                        Expression.Block(
                                            Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                                            context.DiscardResult
                                            ? Expression.Empty()
                                            : Expression.Assign(value, parserCompileResult.Value)
                                            )
                                        )
                                    )
                                ),
                            Expression.IfThen(
                                Expression.Not(success),
                                context.ResetPosition(start)
                                )
                            )
                        )
                    );

            result.Body.Add(block);

            return result;
        }
    }
}
