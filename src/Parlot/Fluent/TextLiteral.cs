using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class TextLiteral : Parser<string>
    {
        private readonly StringComparer _comparer;
        private readonly bool _skipWhiteSpace;

        public TextLiteral(string text, StringComparer comparer = null, bool skipWhiteSpace = true)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            _comparer = comparer;
            _skipWhiteSpace = skipWhiteSpace;
        }

        public string Text { get; }

        public override bool Parse(ParseContext context, ref ParseResult<string> result)
        {
            context.EnterParser(this);

            if (_skipWhiteSpace)
            {
                context.SkipWhiteSpace();
            }

            var start = context.Scanner.Cursor.Offset;

            if (context.Scanner.ReadText(Text, _comparer))
            {
                result.Set(start, context.Scanner.Cursor.Offset, Text);
                return true;
            }
            
            return false;
        }

        public override CompileResult Compile(Expression parseContext)
        {
            var variables = new List<ParameterExpression>();
            var body = new List<Expression>();
            var success = Expression.Variable(typeof(bool), "textSuccess");
            var value = Expression.Variable(typeof(string), "textValue");

            variables.Add(success);
            variables.Add(value);

            //if (_skipWhiteSpace)
            //{
            //    context.SkipWhiteSpace();
            //}
            
            if (_skipWhiteSpace)
            {
                var skipWhiteSpaceMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.SkipWhiteSpace), Array.Empty<Type>());
                body.Add(Expression.Call(parseContext, ExpressionHelper.ParserContext_SkipWhiteSpaceMethod));
            }

            //if (context.Scanner.ReadText(Text, _comparer, null))
            //{
            //    success = true;
            //    value = Text;
            //}
            //{
            //    success = false;
            //    value = null;
            //}

            var ifReadText = Expression.IfThenElse(
                Expression.Call(
                    Expression.Field(parseContext, "Scanner"),
                    ExpressionHelper.Scanner_ReadText,
                    Expression.Constant(Text, typeof(string)),
                    Expression.Constant(_comparer, typeof(StringComparer)),
                    Expression.Constant(null, typeof(TokenResult))
                    ),
                Expression.Block(
                    Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                    Expression.Assign(value, Expression.Constant(Text, typeof(string)))
                    ),
                Expression.Block(
                    Expression.Assign(success, Expression.Constant(false, typeof(bool))),
                    Expression.Assign(value, Expression.Constant(null, typeof(string)))
                    )
                );

            body.Add(ifReadText);

            return new CompileResult(variables, body, success, value);
        }
    }
}
