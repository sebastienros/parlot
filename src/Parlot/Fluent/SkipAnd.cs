using System;

namespace Parlot.Fluent
{
    using Compilation;
    using System.Linq.Expressions;

    public sealed class SkipAnd<A, T> : Parser<T>, ICompilable
    {
        private readonly Parser<A> _parser1;
        private readonly Parser<T> _parser2;

        public SkipAnd(Parser<A> parser1, Parser<T> parser2)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            ParseResult<A> _ = new();
            if (_parser1.Parse(context, ref _))
            {
                var parseResult2 = new ParseResult<T>();

                if (_parser2.Parse(context, ref parseResult2))
                {
                    result.Set(start.Offset, parseResult2.End, parseResult2.Value);
                    return true;
                }

                context.Scanner.Cursor.ResetPosition(start);
            }

            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T)));

            // T value;
            //
            // parse1 instructions
            // 
            // var start = context.Scanner.Cursor.Position;
            //
            // parse1 instructions
            //
            // if (parser1.Success)
            // {
            //    parse2 instructions
            //   
            //    if (parser2.Success)
            //    {
            //       success = true;
            //       value = parse2.Value;
            //    }
            //    else
            //    {
            //        context.Scanner.Cursor.ResetPosition(start);
            //    }
            // }

            // var start = context.Scanner.Cursor.Position;

            var start = context.DeclarePositionVariable(result);

            var parser1CompileResult = _parser1.Build(context);
            var parser2CompileResult = _parser2.Build(context);

            result.Body.Add(
                Expression.Block(
                    parser1CompileResult.Variables,
                    Expression.Block(parser1CompileResult.Body),
                    Expression.IfThen(
                        parser1CompileResult.Success,
                        Expression.Block(
                            parser2CompileResult.Variables,
                            Expression.Block(parser2CompileResult.Body),
                            Expression.IfThenElse(
                                parser2CompileResult.Success,
                                Expression.Block(
                                    context.DiscardResult
                                    ? Expression.Empty()
                                    : Expression.Assign(value, parser2CompileResult.Value),
                                    Expression.Assign(success, Expression.Constant(true, typeof(bool)))
                                ),
                                context.ResetPosition(start)
                            )
                        )
                    )
                )
            );

            return result;
        }
    }
}
