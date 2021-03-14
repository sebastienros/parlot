using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class AndSkip<T, U> : Parser<T>, ICompilable
    {
        private readonly Parser<T> _parser1;
        private readonly Parser<U> _parser2;

        public AndSkip(Parser<T> parser1, Parser<U> parser2)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            if (_parser1.Parse(context, ref result))
            {
                ParseResult<U> _ = new();
                if (_parser2.Parse(context, ref _))
                {
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
            //    value = parse1.Value;
            //    
            //    parse2 instructions
            //   
            //    if (parser2.Success)
            //    {
            //       success = true;
            //    }
            //    else
            //    {
            //        context.Scanner.Cursor.ResetPosition(start);
            //    }
            // }

            // var start = context.Scanner.Cursor.Position;

            var start = Expression.Variable(typeof(TextPosition), $"start{context.Counter}");
            result.Variables.Add(start);

            result.Body.Add(Expression.Assign(start, Expression.Property(Expression.Field(Expression.Field(context.ParseContext, "Scanner"), "Cursor"), "Position")));

            var parser1CompileResult = _parser1.Build(context);
            var parser2CompileResult = _parser2.Build(context);

            result.Body.Add(
                Expression.Block(
                    parser1CompileResult.Variables,
                    Expression.Block(parser1CompileResult.Body),
                    Expression.IfThen(
                        parser1CompileResult.Success,
                        Expression.Block(
                            context.DiscardResult ? Expression.Empty() : Expression.Assign(value, parser1CompileResult.Value),
                            Expression.Block(
                            parser2CompileResult.Variables,
                            Expression.Block(parser2CompileResult.Body),
                            Expression.IfThenElse(
                                parser2CompileResult.Success,
                                Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                                Expression.Call(Expression.Field(Expression.Field(context.ParseContext, "Scanner"), "Cursor"), typeof(Cursor).GetMethod("ResetPosition"), start)
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
