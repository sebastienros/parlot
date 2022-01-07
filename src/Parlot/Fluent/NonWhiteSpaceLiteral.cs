using Parlot.Compilation;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class NonWhiteSpaceLiteral : Parser<TextSpan>, ICompilable
    {
        private readonly bool _includeNewLines;

        public NonWhiteSpaceLiteral(bool includeNewLines = true)
        {
            _includeNewLines = includeNewLines;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
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

            result.Set(start, end, new TextSpan(context.Scanner.Buffer, start, end - start));
            return true;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(TextSpan)));

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
            //         value = new TextSpan(context.Scanner.Buffer, start, end - start);
            //         success = true;
            //     }
            // }

            var start = Expression.Parameter(typeof(int));
            var end = Expression.Parameter(typeof(int));

            result.Body.Add(
                Expression.IfThen(
                    Expression.Not(context.Eof()),
                    Expression.Block(
                        new ParameterExpression [] { start, end },
                        Expression.Assign(start, context.Offset()),
                        _includeNewLines
                            ? context.ReadNonWhiteSpaceOrNewLine()
                            : context.ReadNonWhiteSpace(),
                        Expression.Assign(end, context.Offset()),
                        Expression.IfThen(
                            Expression.NotEqual(start, end),
                            Expression.Block(
                                Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                                context.DiscardResult
                                    ? Expression.Empty()
                                    : Expression.Assign(value, context.NewTextSpan(context.Buffer(), start, Expression.Subtract(end, start))
                                )
                            )
                    )))
            );

            return result;
        }
    }
}
