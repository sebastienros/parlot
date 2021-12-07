using Parlot.Compilation;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class Separated<U, T, TParseContext> : Parser<List<T>, TParseContext>, ICompilable<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Parser<U, TParseContext> _separator;
        private readonly Parser<T, TParseContext> _parser;

        public Separated(Parser<U, TParseContext> separator, Parser<T, TParseContext> parser)
        {
            _separator = separator ?? throw new ArgumentNullException(nameof(separator));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<List<T>> result)
        {
            context.EnterParser(this);

            List<T> results = null;

            var start = 0;
            var end = context.Scanner.Cursor.Position;

            var first = true;
            var parsed = new ParseResult<T>();
            var separatorResult = new ParseResult<U>();

            while (true)
            {
                if (!first)
                {
                    if (!_separator.Parse(context, ref separatorResult))
                    {
                        break;
                    }
                }

                if (!_parser.Parse(context, ref parsed))
                {
                    if (!first)
                    {
                        // A separator was found, but not followed by another value.
                        // It's still succesful if there was one value parsed, but we reset the cursor to before the separator
                        context.Scanner.Cursor.ResetPosition(end);
                        break;
                    }

                    return false;
                }
                else
                {
                    end = context.Scanner.Cursor.Position;
                }
                 
                if (first)
                {
                    results = new List<T>();
                    start = parsed.Start;
                    first = false;
                }
                
                results.Add(parsed.Value);
            }

            result = new ParseResult<List<T>>(start, end.Offset, results);
            return true;
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.New(typeof(List<T>)));
            
            var end = context.DeclarePositionVariable(result);

            // value = new List<T>();
            //
            // while (true)
            // {
            //   parse1 instructions
            // 
            //   if (parser1.Success)
            //   {
            //      success = true;
            //      value.Add(parse1.Value);
            //      end = currenPosition;
            //   }
            //   else
            //   {
            //      break;
            //   }
            //   
            //   parseSeparatorExpression with conditional break
            //
            //   if (context.Scanner.Cursor.Eof)
            //   {
            //      break;
            //   }
            // }
            // 
            // resetPosition(end);
            // 

            var parserCompileResult = _parser.Build(context);
            var breakLabel = Expression.Label("break");

            var separatorCompileResult = _separator.Build(context);

            var block = Expression.Block(
                parserCompileResult.Variables,
                Expression.Loop(
                    Expression.Block(
                        Expression.Block(parserCompileResult.Body),
                        Expression.IfThenElse(
                            parserCompileResult.Success,
                            Expression.Block(
                                context.DiscardResult
                                ? Expression.Empty()
                                : Expression.Call(value, typeof(List<T>).GetMethod("Add"), parserCompileResult.Value),
                                Expression.Assign(success, Expression.Constant(true)),
                                Expression.Assign(end, context.Position())
                                ),
                            Expression.Break(breakLabel)
                            ),
                        Expression.Block(
                            separatorCompileResult.Variables,
                            Expression.Block(separatorCompileResult.Body),
                            Expression.IfThen(
                                Expression.Not(separatorCompileResult.Success),
                                Expression.Break(breakLabel)
                                )
                            ),
                        Expression.IfThen(
                            context.Eof(),
                            Expression.Break(breakLabel)
                            )
                        ),
                    breakLabel),
                context.ResetPosition(end)
                );

            result.Body.Add(block);

            return result;
        }
    }
}
