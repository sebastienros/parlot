using Parlot.Compilation;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    /// <summary>
    /// Ensure the given parser is valid based on a condition, and backtracks if not.
    /// </summary>
    /// <typeparam name="T">The output parser type.</typeparam>
    /// <typeparam name="TParseContext">The parse context type.</typeparam>
    /// <typeparam name="TChar">The char or byte type.</typeparam>
    public sealed class When<T, TParseContext, TChar> : Parser<T, TParseContext, TChar>, ICompilable<TParseContext, TChar>
    where TParseContext : ParseContextWithScanner<TChar>
    where TChar : IEquatable<TChar>, IConvertible
    {
        private readonly Func<T, bool> _action;
        private readonly Parser<T, TParseContext> _parser;

        public When(Parser<T, TParseContext> parser, Func<T, bool> action)
        {
            _action = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            var valid = _parser.Parse(context, ref result) && _action(result.Value);

            if (!valid)
            {
                context.Scanner.Cursor.ResetPosition(start);
            }

            return valid;
        }

        public CompilationResult Compile(CompilationContext<TParseContext, TChar> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T)));

            var parserCompileResult = _parser.Build(context, requireResult: true);

            // success = false;
            // value = default;
            // start = context.Scanner.Cursor.Position;
            // parser instructions
            // 
            // if (parser.Success && _action(value))
            // {
            //   success = true;
            //   value = parser.Value;
            // }
            // else
            // {
            //    context.Scanner.Cursor.ResetPosition(start);
            // }
            //

            var start = context.DeclarePositionVariable(result);

            var block = Expression.Block(
                    parserCompileResult.Variables,
                    parserCompileResult.Body
                    .Append(
                        Expression.IfThenElse(
                            Expression.AndAlso(
                                parserCompileResult.Success,
                                Expression.Invoke(Expression.Constant(_action), new[] { parserCompileResult.Value })
                                ),
                            Expression.Block(
                                Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                                context.DiscardResult
                                    ? Expression.Empty()
                                    : Expression.Assign(value, parserCompileResult.Value)
                                ),
                            context.ResetPosition(start)
                            )
                        )
                    );


            result.Body.Add(block);

            return result;
        }
    }
}
