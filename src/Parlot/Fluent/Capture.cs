using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class Capture<T> : Parser<TextSpan>
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

        public override CompileResult Compile(CompilationContext context)
        {
            var variables = new List<ParameterExpression>();
            var body = new List<Expression>();
            var success = Expression.Variable(typeof(bool), $"success{++context.Counter}");
            var value = Expression.Variable(typeof(TextSpan), $"value{context.Counter}");

            variables.Add(success);
            variables.Add(value);

            body.Add(Expression.Assign(success, Expression.Constant(false, typeof(bool))));

            // var start = context.Scanner.Cursor.Position;

            var start = Expression.Variable(typeof(TextPosition), $"start{context.Counter}");
            variables.Add(start);

            body.Add(Expression.Assign(start, ExpressionHelper.Position(context.ParseContext)));

            var ignoreResults = context.IgnoreResults;
            context.IgnoreResults = true;

            var parserCompileResult = _parser.Compile(context);

            context.IgnoreResults = ignoreResults;

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

            body.Add(
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

            return new CompileResult(variables, body, success, value);
        }
    }
}
