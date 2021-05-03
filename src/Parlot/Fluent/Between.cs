﻿using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class Between<A, T, B> : Parser<T>, ICompilable
    {
        private readonly Parser<T> _parser;
        private readonly Parser<A> _before;
        private readonly Parser<B> _after;

        public Between(Parser<A> before, Parser<T> parser, Parser<B> after)
        {
            _before = before ?? throw new ArgumentNullException(nameof(before));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _after = after ?? throw new ArgumentNullException(nameof(after));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
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

        public CompilationResult Compile(CompilationContext context)
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
