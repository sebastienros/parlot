using Parlot.Compilation;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class Capture<T> : Parser<TextSpan>, ICompilable
    {
        private readonly Parser<T> _parser;

        public Capture(Parser<T> parser)
        {
            _parser = parser;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            ParseResult<T> _ = new();

            // Did parser succeed.
            if (_parser.Parse(context, ref _))
            {
                var end = context.Scanner.Cursor.Offset;
                var length = end - start.Offset;

                result.Set(start.Offset, end, new TextSpan(context.Scanner.Buffer, start.Offset, length));

                return true;
            }

            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(TextSpan)));

            // var start = context.Scanner.Cursor.Position;
            var start = context.DeclarePositionVariable(result);

            var ignoreResults = context.DiscardResult;
            context.DiscardResult = true;

            var parserCompileResult = _parser.Build(context);

            context.DiscardResult = ignoreResults;

            // parse1 instructions
            //
            // if (parser1.Success)
            // {
            //     var end = context.Scanner.Cursor.Offset;
            //     var length = end - start.Offset;
            //   
            //     value = new TextSpan(context.Scanner.Buffer, start.Offset, length);
            //   
            //     success = true;
            // }
            // else
            // {
            //     context.Scanner.Cursor.ResetPosition(start);
            // }

            var startOffset = context.Offset(start);

            result.Body.Add(
                Expression.Block(
                    parserCompileResult.Variables,
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThenElse(
                        parserCompileResult.Success,
                        Expression.Block(
                            context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Assign(value,
                                context.NewTextSpan(
                                    context.Buffer(),
                                    startOffset,
                                    Expression.Subtract(context.Offset(), startOffset)
                                    )),
                            Expression.Assign(success, Expression.Constant(true, typeof(bool)))
                            ),
                        context.ResetPosition(start)
                    )
                )
            );

            return result;
        }
    }
}
