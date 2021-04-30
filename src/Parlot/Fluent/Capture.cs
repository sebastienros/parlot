using Parlot.Compilation;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class Capture<T, TParseContext> : Parser<TextSpan, TParseContext>, ICompilable<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly IParser<T, TParseContext> _parser;

        public Capture(IParser<T, TParseContext> parser)
        {
            _parser = parser;
        }

        public override bool Parse(TParseContext context, ref ParseResult<TextSpan> result)
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

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(TextSpan)));

            // var start = context.Scanner.Cursor.Position;

            var start = Expression.Variable(typeof(TextPosition), $"start{context.NextNumber}");
            result.Variables.Add(start);

            result.Body.Add(Expression.Assign(start, context.Position()));

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
                            Expression.Assign(value,
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
