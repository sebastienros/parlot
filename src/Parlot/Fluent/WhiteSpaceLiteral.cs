using Parlot.Compilation;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class WhiteSpaceLiteral : Parser<TextSpan>, ICompilable
    {
        private readonly bool _includeNewLines;

        public WhiteSpaceLiteral(bool includeNewLines)
        {
            _includeNewLines = includeNewLines;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Offset;

            if (_includeNewLines)
            {
                context.Scanner.SkipWhiteSpaceOrNewLine();
            }
            else
            {
                context.Scanner.SkipWhiteSpace();
            }

            var end = context.Scanner.Cursor.Offset;

            result.Set(start, context.Scanner.Cursor.Offset, new TextSpan(context.Scanner.Buffer, start, end - start));
            return true;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            _ = context.DeclareSuccessVariable(result, true);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(TextSpan)));

            var start = Expression.Parameter(typeof(int));
            result.Variables.Add(start);
            result.Body.Add(Expression.Assign(start, context.Offset()));

            result.Body.Add(
                _includeNewLines
                    ? context.SkipWhiteSpaceOrNewLine()
                    : context.SkipWhiteSpace()
                );

            var end = Expression.Parameter(typeof(int));
            result.Variables.Add(end);
            result.Body.Add(Expression.Assign(end, context.Offset()));

            result.Body.Add(Expression.Assign(value, context.NewTextSpan(context.Buffer(), start, Expression.Subtract(end, start))));

            return result;
        }
    }
}
