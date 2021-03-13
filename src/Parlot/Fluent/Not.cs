using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class Not<T> : Parser<T>, ICompilable
    {
        private readonly Parser<T> _parser;

        public Not(Parser<T> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            if (!_parser.Parse(context, ref result))
            {
                return true;
            }

            context.Scanner.Cursor.ResetPosition(start);
            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = result.Success = Expression.Variable(typeof(bool), $"success{++context.Counter}");
            var value = result.Value = Expression.Variable(typeof(T), $"value{context.Counter}");

            result.Variables.Add(success);

            result.Body.Add(Expression.Assign(success, Expression.Constant(false, typeof(bool))));

            if (!context.DiscardResult)
            {
                result.Variables.Add(value);
                result.Body.Add(Expression.Assign(value, Expression.Constant(default(T), typeof(T))));
            }

            // var start = context.Scanner.Cursor.Position;

            var start = Expression.Variable(typeof(TextPosition), $"start{context.Counter}");
            result.Variables.Add(start);
            result.Body.Add(Expression.Assign(start, Expression.Property(Expression.Field(Expression.Field(context.ParseContext, "Scanner"), "Cursor"), "Position")));

            var parserCompileResult = _parser.Build(context);

            // success = false;
            //
            // parser instructions
            // 
            // if (parser.succces)
            // {
            //     context.Scanner.Cursor.ResetPosition(start);
            // }
            // else
            // {
            //     success = true;
            // }
            // 

            result.Body.Add(
                Expression.Block(
                    parserCompileResult.Variables,
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThenElse(
                        parserCompileResult.Success,
                        Expression.Call(Expression.Field(Expression.Field(context.ParseContext, "Scanner"), "Cursor"), typeof(Cursor).GetMethod("ResetPosition"), start),
                        Expression.Assign(success, Expression.Constant(true, typeof(bool)))
                        )
                    )
                );

            return result;
        }
    }
}
