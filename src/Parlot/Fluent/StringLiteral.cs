using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent
{
    public enum StringLiteralQuotes
    {
        Single,
        Double,
        SingleOrDouble
    }

    public sealed class StringLiteral : Parser<TextSpan>
    {
        private readonly StringLiteralQuotes _quotes;
        private readonly bool _skipWhiteSpace;

        public StringLiteral(StringLiteralQuotes quotes, bool skipWhiteSpace = true)
        {
            _quotes = quotes;
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);

            if (_skipWhiteSpace)
            {
                context.SkipWhiteSpace();
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
                var decoded = Character.DecodeString(new TextSpan(context.Scanner.Buffer, start + 1, end - start - 2));

                result.Set(start, end, decoded);
                return true;
            }
            else
            {
                return false;
            }
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
            body.Add(Expression.Assign(value, Expression.Constant(default(TextSpan), typeof(TextSpan))));

            //if (_skipWhiteSpace)
            //{
            //    context.SkipWhiteSpace();
            //}

            if (_skipWhiteSpace)
            {
                var skipWhiteSpaceMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.SkipWhiteSpace), Array.Empty<Type>());
                body.Add(Expression.Call(context.ParseContext, ExpressionHelper.ParserContext_SkipWhiteSpaceMethod));
            }

            // var start = context.Scanner.Cursor.Offset;

            var start = Expression.Variable(typeof(int), $"start{context.Counter}");
            variables.Add(start);

            body.Add(Expression.Assign(start, ExpressionHelper.Offset(context.ParseContext)));

            var parseStringExpression = _quotes switch
            {
                StringLiteralQuotes.Single => Expression.Call(Expression.Field(context.ParseContext, "Scanner"), typeof(Scanner).GetMethod(nameof(Scanner.ReadSingleQuotedString), Array.Empty<Type>())),
                StringLiteralQuotes.Double => Expression.Call(Expression.Field(context.ParseContext, "Scanner"), typeof(Scanner).GetMethod(nameof(Scanner.ReadDoubleQuotedString), Array.Empty<Type>())),
                StringLiteralQuotes.SingleOrDouble => Expression.Call(Expression.Field(context.ParseContext, "Scanner"), typeof(Scanner).GetMethod(nameof(Scanner.ReadQuotedString), Array.Empty<Type>())),
                _ => throw new InvalidOperationException()
            };

            // if (context.Scanner.ReadSingleQuotedString())
            // {
            //     var end = context.Scanner.Cursor.Offset;
            //     success = true;
            //     value = Character.DecodeString(new TextSpan(context.Scanner.Buffer, start + 1, end - start - 2));
            // }

            var end = Expression.Variable(typeof(int), $"end{context.Counter}");

            var decodeStringMethodInfo = typeof(Character).GetMethod("DecodeString", new[] { typeof(TextSpan) });
            var textSpanCtor = typeof(TextSpan).GetConstructor(new[] { typeof(string), typeof(int), typeof(int) });

            body.Add(
                Expression.IfThen(
                    parseStringExpression,
                    Expression.Block(
                        new[] { end },
                        Expression.Assign(end, ExpressionHelper.Offset(context.ParseContext)),
                        Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                        Expression.Assign(value, 
                            Expression.Call(decodeStringMethodInfo, 
                                Expression.New(textSpanCtor,
                                    ExpressionHelper.Buffer(context.ParseContext),
                                    Expression.Add(start, Expression.Constant(1)),
                                    Expression.Subtract(Expression.Subtract(end, start), Expression.Constant(2))
                                    )))
                    )
                ));

            return new CompileResult(variables, body, success, value);
        }
    }
}
