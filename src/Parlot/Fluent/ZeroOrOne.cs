using Parlot.Compilation;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class ZeroOrOne<T> : Parser<T>, ICompilable
    {
        private readonly Parser<T> _parser;

        public ZeroOrOne(Parser<T> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            _parser.Parse(context, ref result);

            return true;
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
                result.Body.Add(Expression.Assign(value, Expression.New(typeof(T))));
            }

            // T value;
            //
            // parse1 instructions
            // 
            // if (parser1.Success)
            // {
            //    results.Add(parse1.Value);
            //    success = true;
            // }
            // 

            var parserCompileResult = _parser.Build(context);

            var block = Expression.Block(
                parserCompileResult.Variables,
                    Expression.Block(
                        Expression.Block(parserCompileResult.Body),
                        Expression.IfThenElse(
                            parserCompileResult.Success,
                            context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Call(value, typeof(List<T>).GetMethod("Add"), parserCompileResult.Value),
                            Expression.Assign(success, Expression.Constant(true))
                            )
                        )
                    );

            result.Body.Add(block);

            return result;
        }
    }
}
