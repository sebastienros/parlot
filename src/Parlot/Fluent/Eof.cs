using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    /// <summary>
    /// Successful when the cursor is at the end of the string.
    /// </summary>
    public sealed class Eof<T> : Parser<T>, ICompilable
    {
        private readonly Parser<T> _parser;

        public Eof(Parser<T> parser)
        {
            _parser = parser;
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            if (_parser.Parse(context, ref result) && context.Scanner.Cursor.Eof)
            {
                return true;
            }

            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T)));

            // parse1 instructions
            // 
            // if (parser1.Success && context.Scanner.Cursor.Eof)
            // {
            //    value = parse1.Value;
            //    success = true;
            // }

            var parserCompileResult = _parser.Build(context);

            result.Body.Add(
                Expression.Block(
                    parserCompileResult.Variables,
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThen(
                        Expression.AndAlso(parserCompileResult.Success, context.Eof()),
                        Expression.Block(
                            context.DiscardResult
                                ? Expression.Empty()
                                : Expression.Assign(value, parserCompileResult.Value),
                            Expression.Assign(success, Expression.Constant(true, typeof(bool)))                            
                            )
                        )
                    )
                );

            return result;
        }
    }
}
