using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class Separated<U, T> : Parser<IReadOnlyList<T>>, ICompilable, ISeekable
    {
        private readonly Parser<U> _separator;
        private readonly Parser<T> _parser;

        public Separated(Parser<U> separator, Parser<T> parser)
        {
            _separator = separator ?? throw new ArgumentNullException(nameof(separator));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            
            if (_parser is ISeekable seekable)
            {
                CanSeek = seekable.CanSeek;
                ExpectedChars = seekable.ExpectedChars;
                SkipWhitespace = seekable.SkipWhitespace;
            }
        }

        public bool CanSeek { get; }

        public char[] ExpectedChars { get; } = [];

        public bool SkipWhitespace { get; }

        public override bool Parse(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
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
                        // It's still successful if there was one value parsed, but we reset the cursor to before the separator
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

            result = new ParseResult<IReadOnlyList<T>>(start, end.Offset, results ?? (IReadOnlyList<T>)Array.Empty<T>());
            return true;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, ExpressionHelper.ArrayEmpty<T>(), typeof(IReadOnlyList<T>));

            var results = context.DeclareVariable<List<T>>(result, $"results{context.NextNumber}");

            var end = context.DeclarePositionVariable(result);

            // success = false;
            //
            // IReadonlyList<T> value = Array.Empty<T>();
            // List<T> results = null;
            //
            // while (true)
            // {
            //   parse1 instructions
            // 
            //   if (parser1.Success)
            //   {
            //      success = true;
            //      if (results == null) results = new List<T>();
            //      results.Add(parse1.Value);
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
                                : Expression.Block(
                                    Expression.IfThen(
                                        Expression.Equal(results, Expression.Constant(null, typeof(List<T>))),
                                        Expression.Block(
                                            Expression.Assign(results, ExpressionHelper.New<List<T>>()),
                                            Expression.Assign(value, results)
                                            )
                                        ),
                                    Expression.Call(results, typeof(List<T>).GetMethod("Add"), parserCompileResult.Value)
                                    ),
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
