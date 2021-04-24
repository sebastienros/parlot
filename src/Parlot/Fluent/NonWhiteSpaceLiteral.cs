using Parlot.Compilation;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class NonWhiteSpaceLiteral : Parser<TextSpan>, ICompilable
    {
        private readonly bool _skipWhiteSpace;
        private readonly bool _includeNewLines;

        public NonWhiteSpaceLiteral(bool skipWhiteSpace = true, bool includeNewLines = false)
        {
            _skipWhiteSpace = skipWhiteSpace;
            _includeNewLines = includeNewLines;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            if (_skipWhiteSpace)
            {
                context.SkipWhiteSpace();
            }

            if (context.Scanner.Cursor.Eof)
            {
                return false;
            }

            var start = context.Scanner.Cursor.Offset;

            if (_includeNewLines)
            {
                context.Scanner.ReadNonWhiteSpace();
            }
            else
            {
                context.Scanner.ReadNonWhiteSpaceOrNewLine();
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

            //if (_skipWhiteSpace)
            //{
            //    context.SkipWhiteSpace();
            //}

            if (_skipWhiteSpace)
            {
                result.Body.Add(context.ParserSkipWhiteSpace());
            }

            // if (!context.Scanner.Cursor.Eof)
            // {
            //     var start = context.Scanner.Cursor.Offset;
            //     
            //     [if (_includeNewLines)]
            //         context.Scanner.ReadNonWhiteSpace();
            //     [else]
            //         context.Scanner.ReadNonWhiteSpaceOrNewLine();
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
                            ? context.ReadNonWhiteSpace()
                            : context.ReadNonWhiteSpaceOrNewLine(),
                        Expression.Assign(end, context.Offset()),
                        Expression.IfThen(
                            Expression.NotEqual(start, end),
                            Expression.Block(
                                Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                                Expression.Assign(value, context.NewTextSpan(context.Buffer(), start, Expression.Subtract(end, start))
                                )
                            )
                    )))
            );

            return result;
        }
    }
}
