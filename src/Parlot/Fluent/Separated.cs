using Parlot.Compilation;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class Separated<U, T> : Parser<List<T>>, ICompilable
    {
        private readonly Parser<U> _separator;
        private readonly Parser<T> _parser;

        private readonly bool _separatorIsChar;
        private readonly char _separatorChar;
        private readonly bool _separatorWhiteSpace;

        public Separated(Parser<U> separator, Parser<T> parser)
        {
            _separator = separator ?? throw new ArgumentNullException(nameof(separator));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));

            // TODO: more optimization could be done for other literals by creating different implementations of this class instead of doing 
            // ifs in the Parse method. Then the builders could check the kind of literal used and return the correct implementation.

            if (separator is CharLiteral literal)
            {
                _separatorIsChar = true;
                _separatorChar = literal.Char;
                _separatorWhiteSpace = literal.SkipWhiteSpace;
            }
        }

        public override bool Parse(ParseContext context, ref ParseResult<List<T>> result)
        {
            context.EnterParser(this);

            List<T> results = null;

            var start = 0;
            var end = 0;

            var first = true;
            var parsed = new ParseResult<T>();
            var separatorResult = new ParseResult<U>();

            while (true)
            {
                if (!_parser.Parse(context, ref parsed))
                {
                    if (!first)
                    {
                        break;
                    }

                    // A parser that returns false is reponsible for resetting the position.
                    // Nothing to do here since the inner parser is already failing and resetting it.
                    return false;
                }

                if (first)
                {
                    start = parsed.Start;
                }

                end = parsed.End;
                results ??= new List<T>();
                results.Add(parsed.Value);

                if (_separatorWhiteSpace)
                {
                    context.SkipWhiteSpace();
                }

                if (_separatorIsChar)
                {
                    if (!context.Scanner.ReadChar(_separatorChar))
                    {
                        break;
                    }
                }
                else if (!_separator.Parse(context, ref separatorResult))
                {
                    break;
                }
            }

            result = new ParseResult<List<T>>(start, end, results);
            return true;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.New(typeof(List<T>)));

            // value = new List<T>();
            //
            // while (true)
            // {
            //   parse1 instructions
            // 
            //   if (parser1.Success)
            //   {
            //      results.Add(parse1.Value);
            //   }
            //   else
            //   {
            //      break;
            //   }
            //
            //   if (context.Scanner.Cursor.Eof)
            //   {
            //      break;
            //   }
            // }
            //
            // success = true;

            var parserCompileResult = _parser.Build(context);
            var breakLabel = Expression.Label("break");

            Expression parseSeparatorExpression;

            if (_separatorIsChar)
            {
                parseSeparatorExpression = Expression.IfThen(
                    Expression.Not(context.ReadChar(_separatorChar)),
                    Expression.Break(breakLabel)
                    );
            }
            else
            {
                var separatorCompileResult = _separator.Build(context);

                parseSeparatorExpression = Expression.Block(
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThen(
                            Expression.Not(separatorCompileResult.Success),
                            Expression.Break(breakLabel)
                            )
                    );
            }

            if (_separatorWhiteSpace)
            {
                var skipWhiteSpaceMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.SkipWhiteSpace), Array.Empty<Type>());
                result.Body.Add(Expression.Call(context.ParseContext, ExpressionHelper.ParserContext_SkipWhiteSpaceMethod));
            }

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
                                Expression.Assign(success, Expression.Constant(true))
                                ),
                            Expression.Break(breakLabel)
                            ),
                        parseSeparatorExpression,
                        Expression.IfThen(
                            context.Eof(),
                            Expression.Break(breakLabel)
                            )
                        ),
                    breakLabel)
                );

            result.Body.Add(block);

            return result;
        }
    }
}
