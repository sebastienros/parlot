using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class OneOrMany<T> : Parser<List<T>>
    {
        private readonly Parser<T> _parser;

        public OneOrMany(Parser<T> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(ParseContext context, ref ParseResult<List<T>> result)
        {
            context.EnterParser(this);

            var parsed = new ParseResult<T>();

            if (!_parser.Parse(context, ref parsed))
            {
                return false;
            }

            var start = parsed.Start;
            var results = new List<T>();

            int end = 0;

            do
            {
                end = parsed.End;
                results.Add(parsed.Value);

            } while (_parser.Parse(context, ref parsed));

            result = new ParseResult<List<T>>(start, end, results);
            return true;
        }

        public override CompileResult Compile(CompilationContext context)
        {
            var variables = new List<ParameterExpression>();
            var body = new List<Expression>();
            var success = Expression.Variable(typeof(bool), $"success{++context.Counter}");
            var value = Expression.Variable(typeof(List<T>), $"value{context.Counter}");

            variables.Add(success);

            body.Add(Expression.Assign(success, Expression.Constant(false, typeof(bool))));

            if (!context.IgnoreResults)
            {
                variables.Add(value);
                body.Add(Expression.Assign(value, Expression.New(typeof(List<T>))));
            }

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
            // if (value.Count > 0)
            // {
            //     success = true;
            // }
            // 

            var parserCompileResult = _parser.Compile(context);

            var breakLabel = Expression.Label("break");

            var block = Expression.Block(
                parserCompileResult.Variables,
                Expression.Loop(
                    Expression.Block(
                        Expression.Block(parserCompileResult.Body),
                        Expression.IfThenElse(
                            parserCompileResult.Success,
                            Expression.Block(
                                context.IgnoreResults
                                ? Expression.Empty()
                                : Expression.Call(value, typeof(List<T>).GetMethod("Add"), parserCompileResult.Value),
                                Expression.Assign(success, Expression.Constant(true))
                                ),
                            Expression.Break(breakLabel)
                            ),
                        Expression.IfThen(
                            ExpressionHelper.Eof(context.ParseContext),
                            Expression.Break(breakLabel)
                            )),
                    breakLabel)
            );

            body.Add(block);

            return new CompileResult(variables, body, success, value);
        }
    }
}
