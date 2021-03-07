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

            var success = result.Success = Expression.Variable(typeof(bool), $"success{++context.Counter}");
            var value = result.Value = Expression.Variable(typeof(TextSpan), $"value{context.Counter}");

            result.Variables.Add(success);
            result.Variables.Add(value);

            result.Body.Add(Expression.Assign(success, Expression.Constant(false, typeof(bool))));

            // var start = context.Scanner.Cursor.Position;

            var start = Expression.Variable(typeof(TextPosition), $"start{context.Counter}");
            result.Variables.Add(start);

            result.Body.Add(Expression.Assign(start, ExpressionHelper.Position(context.ParseContext)));

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

            var textSpanCtor = typeof(TextSpan).GetConstructor(new[] { typeof(string), typeof(int), typeof(int) });
            var startOffset = Expression.Field(start, nameof(TextPosition.Offset));

            result.Body.Add(
                Expression.Block(
                    parserCompileResult.Variables,
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThenElse(
                        parserCompileResult.Success,
                        Expression.Block(
                            Expression.Assign(value,
                                Expression.New(textSpanCtor,
                                    ExpressionHelper.Buffer(context.ParseContext),
                                    startOffset,
                                    Expression.Subtract(ExpressionHelper.Offset(context.ParseContext), startOffset)
                                    )),
                            Expression.Assign(success, Expression.Constant(true, typeof(bool)))
                            ),
                        ExpressionHelper.ResetPosition(context.ParseContext, start)
                    )
                )
            );

            return result;
        }
    }
}
