using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    /// <summary>
    /// OneOf the inner choices when all parsers return the same type.
    /// We then return the actual result of each parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class OneOf<T> : Parser<T>
    {
        private readonly Parser<T>[] _parsers;

        public OneOf(Parser<T>[] parsers)
        {
            _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));
        }

        public Parser<T>[] Parsers => _parsers;

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            foreach (var parser in _parsers)
            {
                if (parser.Parse(context, ref result))
                {
                    return true;
                }

                // If the choice as a subset of its parsers that succeeded, it might have advanced the cursor
                context.Scanner.Cursor.ResetPosition(start);
            }

            return false;
        }

        public override CompileResult Compile(CompilationContext context)
        {
            var variables = new List<ParameterExpression>();
            var body = new List<Expression>();
            var success = Expression.Variable(typeof(bool), $"success{++context.Counter}");
            var value = Expression.Variable(typeof(T), $"value{context.Counter}");

            variables.Add(success);
            variables.Add(value);

            body.Add(Expression.Assign(success, Expression.Constant(false, typeof(bool))));
            body.Add(Expression.Assign(value, Expression.Constant(default(T), typeof(T))));

            // var start = context.Scanner.Cursor.Position;

            var start = Expression.Variable(typeof(TextPosition), $"start{context.Counter}");
            variables.Add(start);

            body.Add(Expression.Assign(start, Expression.Property(Expression.Field(Expression.Field(context.ParseContext, "Scanner"), "Cursor"), "Position")));

            // parse1 instructions
            // 
            // if (parser1.Success)
            // {
            //    success = true;
            //    value = parse1.Value;
            // }
            // else
            // {
            //   parse2 instructions
            //   
            //   if (parser2.Success)
            //   {
            //      success = true;
            //      value = parse2.Value
            //   }
            //   else
            //   {
            //      success = false;
            //      context.Scanner.Cursor.ResetPosition(start);
            //   }
            // }

            // Initialize the block variable with the inner else statement
            var block = Expression.Block(
                            Expression.Assign(success, Expression.Constant(false, typeof(bool))),
                            Expression.Call(Expression.Field(Expression.Field(context.ParseContext, "Scanner"), "Cursor"), typeof(Cursor).GetMethod("ResetPosition"), start)
                            );

            foreach (var parser in _parsers.Reverse())
            {
                var parserCompileResult = parser.Compile(context);

                block = Expression.Block(
                    parserCompileResult.Variables,
                    parserCompileResult.Body
                    .Append(
                        Expression.IfThenElse(
                            parserCompileResult.Success,
                            Expression.Block(
                                Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                                Expression.Assign(value, parserCompileResult.Value)
                                ),
                            block
                            )
                        )
                    );

            }

            body.Add(block);

            return new CompileResult(variables, body, success, value);
        }
    }

    public sealed class OneOf<A, B, T> : Parser<T>
        where A : T
        where B : T
    {
        private readonly Parser<A> _parserA;
        private readonly Parser<B> _parserB;

        public OneOf(Parser<A> parserA, Parser<B> parserB)
        {
            _parserA = parserA ?? throw new ArgumentNullException(nameof(parserA));
            _parserB = parserB ?? throw new ArgumentNullException(nameof(parserB));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);


            var resultA = new ParseResult<A>();

            var start = context.Scanner.Cursor.Position;

            if (_parserA.Parse(context, ref resultA))
            {
                result.Set(resultA.Start, resultA.End, resultA.Value);

                return true;
            }

            // If the choice as a subset of its parsers that succeeded, it might have advanced the cursor
            context.Scanner.Cursor.ResetPosition(start);

            var resultB = new ParseResult<B>();

            if (_parserB.Parse(context, ref resultB))
            {
                result.Set(resultA.Start, resultA.End, resultA.Value);

                return true;
            }

            // If the choice as a subset of its parsers that succeeded, it might have advanced the cursor
            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }
    }
}
