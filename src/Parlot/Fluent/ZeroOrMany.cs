using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class ZeroOrMany<T> : Parser<IReadOnlyList<T>>, ICompilable, ISeekable
    {
        private readonly Parser<T> _parser;

        public ZeroOrMany(Parser<T> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public bool CanSeek => _parser is ISeekable seekable && seekable.CanSeek;

        public char[] ExpectedChars => _parser is ISeekable seekable ? seekable.ExpectedChars : default;

        public bool SkipWhitespace => _parser is ISeekable seekable && seekable.SkipWhitespace;

        public override bool Parse(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
        {
            context.EnterParser(this);

            List<T> results = null;

            var start = 0;
            var end = 0;

            var first = true;
            var parsed = new ParseResult<T>();

            // TODO: it's not restoring an intermediate failed text position
            // is the inner parser supposed to be clean?

            while (_parser.Parse(context, ref parsed))
            {
                if (first)
                {
                    first = false;
                    start = parsed.Start;
                }

                end = parsed.End;

                results ??= [];
                results.Add(parsed.Value);
            }

            result = new ParseResult<IReadOnlyList<T>>(start, end, results ?? (IReadOnlyList<T>)Array.Empty<T>());
            return true;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var _ = context.DeclareSuccessVariable(result, true);
            var value = context.DeclareValueVariable(result, ExpressionHelper.ArrayEmpty<T>(), typeof(IReadOnlyList<T>));
            
            var results = context.DeclareVariable<List<T>>(result, $"results{context.NextNumber}");

            // success = true;
            //
            // IReadonlyList<T> value = Array.Empty<T>();
            // List<T> results = null;
            //
            // while (true)
            // {
            //
            //   parse1 instructions
            // 
            //   if (parser1.Success)
            //   {
            //      if (results == null) results = new List<T>();
            //      results.Add(parse1.Value);
            //   }
            //   else
            //   {
            //      break;
            //   }
            //
            //   value = results;
            //
            //   if (context.Scanner.Cursor.Eof)
            //   {
            //      break;
            //   }
            // }

            var parserCompileResult = _parser.Build(context);

            var breakLabel = Expression.Label("break");

            var block =
                Expression.Loop(
                    Expression.Block(
                        parserCompileResult.Variables,
                        Expression.Block(parserCompileResult.Body),
                        Expression.IfThenElse(
                            parserCompileResult.Success,
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
                            Expression.Break(breakLabel)
                            ),
                        Expression.IfThen(
                            context.Eof(),
                            Expression.Break(breakLabel)
                            )),
                    breakLabel
                    );

            result.Body.Add(block);

            return result;
        }
    }
}
