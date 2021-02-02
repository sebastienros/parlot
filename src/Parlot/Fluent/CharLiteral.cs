using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class CharLiteral : Parser<char>
    {
        public CharLiteral(char c, bool skipWhiteSpace = true)
        {
            Char = c;
            SkipWhiteSpace = skipWhiteSpace;
        }

        public char Char { get; }

        public bool SkipWhiteSpace { get; }

        public override bool Parse(ParseContext context, ref ParseResult<char> result)
        {
            context.EnterParser(this);

            if (SkipWhiteSpace)
            {
                context.SkipWhiteSpace();
            }

            var start = context.Scanner.Cursor.Offset;

            if (context.Scanner.ReadChar(Char))
            {
                result.Set(start, context.Scanner.Cursor.Offset, Char);
                return true;
            }

            return false;
        }

        public override CompileResult Compile(CompilationContext context)
        {
            var variables = new List<ParameterExpression>();
            var body = new List<Expression>();
            var success = Expression.Variable(typeof(bool), $"success{++context.Counter}");
            var value = Expression.Variable(typeof(char), $"value{context.Counter}");

            variables.Add(success);
            variables.Add(value);

            body.Add(Expression.Assign(success, Expression.Constant(false, typeof(bool))));
            body.Add(Expression.Assign(value, Expression.Constant(default(char), typeof(char))));

            //if (_skipWhiteSpace)
            //{
            //    context.SkipWhiteSpace();
            //}

            if (SkipWhiteSpace)
            {
                var skipWhiteSpaceMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.SkipWhiteSpace), Array.Empty<Type>());
                body.Add(Expression.Call(context.ParseContext, ExpressionHelper.ParserContext_SkipWhiteSpaceMethod));
            }

            // if (context.Scanner.ReadChar(Char))
            // {
            //     success = true;
            //     value = Char;
            // }

            var ifReadText = Expression.IfThen(
                ExpressionHelper.ReadChar(context.ParseContext, Char),
                Expression.Block(
                    Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                    Expression.Assign(value, Expression.Constant(Char, typeof(char)))
                    )
                );

            body.Add(ifReadText);

            return new CompileResult(variables, body, success, value);
        }
    }
}
