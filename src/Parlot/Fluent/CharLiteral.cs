using Parlot.Compilation;
using Parlot.Rewriting;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class CharLiteral : Parser<char>, ICompilable, ISeekable
    {
        public CharLiteral(char c)
        {
            Char = c;
            ExpectedChars = [c];
        }

        public char Char { get; }

        public bool CanSeek { get; } = true;

        public char[] ExpectedChars { get; }

        public bool SkipWhitespace { get; }

        public override bool Parse(ParseContext context, ref ParseResult<char> result)
        {
            context.EnterParser(this);

            var cursor = context.Scanner.Cursor;

            if (cursor.Match(Char))
            {
                var start = cursor.Offset;
                cursor.Advance();
                result.Set(start, cursor.Offset, Char);
                return true;
            }

            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = context.CreateCompilationResult<char>();

            // if (context.Scanner.ReadChar(Char))
            // {
            //     success = true;
            //     value = Char;
            // }

            result.Body.Add(
                Expression.IfThen(
                    context.ReadChar(Char),
                    Expression.Block(
                        Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                        context.DiscardResult
                        ? Expression.Empty()
                        : Expression.Assign(result.Value, Expression.Constant(Char, typeof(char)))
                        )
                    )
            );

            return result;
        }
    }
}
