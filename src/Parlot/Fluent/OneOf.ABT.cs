using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class OneOf<A, B, T, TParseContext> : Parser<T, TParseContext>, ICompilable<TParseContext>
        where TParseContext : ParseContext
        where A : T
        where B : T
    {
        private readonly Parser<A, TParseContext> _parserA;
        private readonly Parser<B, TParseContext> _parserB;

        public OneOf(Parser<A, TParseContext> parserA, Parser<B, TParseContext> parserB)
        {
            _parserA = parserA ?? throw new ArgumentNullException(nameof(parserA));
            _parserB = parserB ?? throw new ArgumentNullException(nameof(parserB));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var resultA = new ParseResult<A>();

            if (_parserA.Parse(context, ref resultA))
            {
                result.Set(resultA.Start, resultA.End, resultA.Value);

                return true;
            }

            var resultB = new ParseResult<B>();

            if (_parserB.Parse(context, ref resultB))
            {
                result.Set(resultB.Start, resultB.End, resultB.Value);

                return true;
            }

            return false;
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T)));

            // T value;
            //
            // parse1 instructions
            // 
            // if (parser1.Success)
            // {
            //    success = true;
            //    value = (T) parse1.Value;
            // }
            // else
            // {
            //   parse2 instructions
            //   
            //   if (parser2.Success)
            //   {
            //      success = true;
            //      value = (T) parse2.Value
            //   }
            // }

            var parser1CompileResult = _parserA.Build(context);
            var parser2CompileResult = _parserB.Build(context);

            result.Body.Add(
                Expression.Block(
                    parser1CompileResult.Variables,
                    Expression.Block(parser1CompileResult.Body),
                    Expression.IfThenElse(
                        parser1CompileResult.Success,
                        Expression.Block(
                            Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                            context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Assign(value, Expression.Convert(parser1CompileResult.Value, typeof(T)))
                            ),
                        Expression.Block(
                            parser2CompileResult.Variables,
                            Expression.Block(parser2CompileResult.Body),
                            Expression.IfThen(
                                parser2CompileResult.Success,
                                Expression.Block(
                                    Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                                    context.DiscardResult
                                    ? Expression.Empty()
                                    : Expression.Assign(value, Expression.Convert(parser2CompileResult.Value, typeof(T)))
                                    )
                                )
                            )
                        )
                    )
                );

            return result;
        }
    }
}
