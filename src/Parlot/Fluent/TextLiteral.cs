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

        public override CompileResult Compile(CompilationContext context)
        {
            var variables = new List<ParameterExpression>();
            var body = new List<Expression>();
            var success = Expression.Variable(typeof(bool), $"success{++context.Counter}");
            var value = Expression.Variable(typeof(string), $"value{context.Counter}");

            variables.Add(success);
            variables.Add(value);

            body.Add(Expression.Assign(success, Expression.Constant(false, typeof(bool))));
            body.Add(Expression.Assign(value, Expression.Constant(default(string), typeof(string))));

            //if (_skipWhiteSpace)
            //{
            //    context.SkipWhiteSpace();
            //}

            if (_skipWhiteSpace)
            {
                var skipWhiteSpaceMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.SkipWhiteSpace), Array.Empty<Type>());
                body.Add(Expression.Call(context.ParseContext, ExpressionHelper.ParserContext_SkipWhiteSpaceMethod));
            }

            // if (context.Scanner.ReadText(Text, _comparer, null))
            // {
            //     success = true;
            //     value = Text;
            // }

            var ifReadText = Expression.IfThen(
                Expression.Call(
                    Expression.Field(context.ParseContext, "Scanner"),
                    ExpressionHelper.Scanner_ReadText,
                    Expression.Constant(Text, typeof(string)),
                    Expression.Constant(_comparer, typeof(StringComparer)),
                    Expression.Constant(null, typeof(TokenResult))
                    ),
                Expression.Block(
                    Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                    Expression.Assign(value, Expression.Constant(Text, typeof(string)))
                    )
                );

            body.Add(ifReadText);

            return new CompileResult(variables, body, success, value);
        }
    }
}
