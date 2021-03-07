using Parlot.Compilation;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    /// <summary>
    /// OneOf the inner choices when all parsers return the same type.
    /// We then return the actual result of each parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class OneOf<T> : Parser<T>, ICompilable
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

            foreach (var parser in _parsers)
            {
                if (parser.Parse(context, ref result))
                {
                    return true;
                }
            }

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
            //   
            //   ...
            // }


            Expression block = Expression.Empty();

            foreach (var parser in _parsers.Reverse())
            {
                var parserCompileResult = parser.Build(context);

                block = Expression.Block(
                    parserCompileResult.Variables,
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThenElse(
                        parserCompileResult.Success,
                        Expression.Block(
                            Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                            context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Assign(value, parserCompileResult.Value)
                            ),
                        block
                        )
                    );
            }

            result.Body.Add(block);

            return result;
        }
    }
}
