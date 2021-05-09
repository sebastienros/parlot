using Parlot.Compilation;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class NonWhiteSpaceLiteral<TParseContext> : Parser<BufferSpan<char>, TParseContext, char>, ICompilable<TParseContext, char>
    where TParseContext : ParseContextWithScanner<Scanner<char>, char>
    {
        private readonly bool _includeNewLines;

        public NonWhiteSpaceLiteral(bool includeNewLines = true)
        {
            _includeNewLines = includeNewLines;
        }

        public override bool Parse(TParseContext context, ref ParseResult<BufferSpan<char>> result)
        {
            if (context.Scanner.Cursor.Eof)
            {
                return false;
            }

            var start = context.Scanner.Cursor.Offset;

            if (_includeNewLines)
            {
                context.Scanner.ReadNonWhiteSpaceOrNewLine();
            }
            else
            {
                context.Scanner.ReadNonWhiteSpace();
            }

            var end = context.Scanner.Cursor.Offset;

            if (start == end)
            {
                return false;
            }

            result.Set(start, end, context.Scanner.Buffer.SubBuffer(start, end - start));
            return true;
        }

        public CompilationResult Compile(CompilationContext<TParseContext, char> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(BufferSpan<char>)));

            // if (!context.Scanner.Cursor.Eof)
            // {
            //     var start = context.Scanner.Cursor.Offset;
            //     
            //     [if (_includeNewLines)]
            //         context.Scanner.ReadNonWhiteSpaceOrNewLine();
            //     [else]
            //         context.Scanner.ReadNonWhiteSpace();
            //     
            //     var end = context.Scanner.Cursor.Offset;
            //     
            //     if (start != end)
            //     {
            //         value = new BufferSpan<char>(context.Scanner.Buffer, start, end - start);
            //         success = true;
            //     }
            // }

            var start = Expression.Parameter(typeof(int));
            var end = Expression.Parameter(typeof(int));

            result.Body.Add(
                Expression.IfThen(
                    Expression.Not(context.Eof()),
                    Expression.Block(
                        new ParameterExpression[] { start, end },
                        Expression.Assign(start, context.Offset()),
                        _includeNewLines
                            ? context.ReadNonWhiteSpaceOrNewLine()
                            : context.ReadNonWhiteSpace(),
                        Expression.Assign(end, context.Offset()),
                        Expression.IfThen(
                            Expression.NotEqual(start, end),
                            Expression.Block(
                                Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                                Expression.Assign(value, context.SubBufferSpan(start, Expression.Subtract(end, start))
                                )
                            )
                    )))
            );

            return result;
        }
    }
}
