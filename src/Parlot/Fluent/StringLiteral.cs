using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public enum StringLiteralQuotes
    {
        Single,
        Double,
        SingleOrDouble
    }

    public sealed class StringLiteral<TParseContext> : Parser<BufferSpan<char>, TParseContext, char>, ICompilable<TParseContext, char>
    where TParseContext : ParseContextWithScanner<Scanner<char>, char>
    {
        private readonly StringLiteralQuotes _quotes;
        private readonly bool _skipWhiteSpace;

        public StringLiteral(StringLiteralQuotes quotes, bool skipWhiteSpace = true)
        {
            _quotes = quotes;
            _skipWhiteSpace = skipWhiteSpace && typeof(TParseContext).IsAssignableFrom(typeof(StringParseContext));
        }

        public override bool Parse(TParseContext context, ref ParseResult<BufferSpan<char>> result)
        {
            context.EnterParser(this);

            if (_skipWhiteSpace)
            {
                ((StringParseContext)(object)context).SkipWhiteSpace();
            }

            var start = context.Scanner.Cursor.Offset;

            var success = _quotes switch
            {
                StringLiteralQuotes.Single => context.Scanner.ReadSingleQuotedString(),
                StringLiteralQuotes.Double => context.Scanner.ReadDoubleQuotedString(),
                StringLiteralQuotes.SingleOrDouble => context.Scanner.ReadQuotedString(),
                _ => false
            };

            var end = context.Scanner.Cursor.Offset;

            if (success)
            {
                // Remove quotes
                var decoded = Character.DecodeString(context.Scanner.Buffer.SubBuffer(start + 1, end - start - 2));

                result.Set(start, end, decoded);
                return true;
            }
            else
            {
                return false;
            }
        }

        public CompilationResult Compile(CompilationContext<TParseContext, char> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(BufferSpan<char>)));

            //if (_skipWhiteSpace)
            //{
            //    context.SkipWhiteSpace();
            //}

            if (_skipWhiteSpace)
            {
                result.Body.Add(context.ParserSkipWhiteSpace());
            }

            // var start = context.Scanner.Cursor.Offset;

            var start = Expression.Variable(typeof(int), $"start{context.NextNumber}");
            result.Variables.Add(start);

            result.Body.Add(Expression.Assign(start, context.Offset()));

            var parseStringExpression = _quotes switch
            {
                StringLiteralQuotes.Single => context.ReadSingleQuotedString(),
                StringLiteralQuotes.Double => context.ReadDoubleQuotedString(),
                StringLiteralQuotes.SingleOrDouble => context.ReadQuotedString(),
                _ => throw new InvalidOperationException()
            };

            // if (context.Scanner.ReadSingleQuotedString())
            // {
            //     var end = context.Scanner.Cursor.Offset;
            //     success = true;
            //     value = Character.DecodeString(new BufferSpan<char>(context.Scanner.Buffer, start + 1, end - start - 2));
            // }

            var end = Expression.Variable(typeof(int), $"end{context.NextNumber}");

            var decodeStringMethodInfo = typeof(Character).GetMethod("DecodeString", new[] { typeof(BufferSpan<char>) });

            result.Body.Add(
                Expression.IfThen(
                    parseStringExpression,
                    Expression.Block(
                        new[] { end },
                        Expression.Assign(end, context.Offset()),
                        Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                        context.DiscardResult
                        ? Expression.Empty()
                        : Expression.Assign(value,
                            Expression.Call(decodeStringMethodInfo,
                                context.SubBufferSpan(
                                    Expression.Add(start, Expression.Constant(1)),
                                    Expression.Subtract(Expression.Subtract(end, start), Expression.Constant(2))
                                    )))
                    )
                ));

            return result;
        }
    }
}
