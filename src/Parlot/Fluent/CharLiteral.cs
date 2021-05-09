using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class CharLiteral<T, TParseContext> : Parser<T, TParseContext, T>, ICompilable<TParseContext, T>
    where TParseContext : ParseContextWithScanner<Scanner<T>, T>
    where T : IEquatable<T>, IConvertible
    {
        public CharLiteral(T c)
        {
            Char = c;
        }

        public T Char { get; }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Offset;

            if (context.Scanner.ReadChar(Char))
            {
                result.Set(start, context.Scanner.Cursor.Offset, Char);
                return true;
            }

            return false;
        }

        public CompilationResult Compile(CompilationContext<TParseContext, T> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(char)));

            // if (context.Scanner.ReadChar(Char))
            // {
            //     success = true;
            //     value = Char;
            // }

            result.Body.Add(
                Expression.IfThen(
                    context.ReadChar(Char),
                    Expression.Block(
                        Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                        context.DiscardResult
                        ? Expression.Empty()
                        : Expression.Assign(value, Expression.Constant(Char, typeof(char)))
                        )
                    )
            );

            return result;
        }
    }
}
