﻿using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class TextLiteral : Parser<string>, ICompilable
    {
        private readonly StringComparer _comparer;

        public TextLiteral(string text, StringComparer comparer = null)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            _comparer = comparer;
        }

        public string Text { get; }

        public override bool Parse(ParseContext context, ref ParseResult<string> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Offset;

            if (context.Scanner.ReadText(Text, _comparer))
            {
                result.Set(start, context.Scanner.Cursor.Offset, Text);
                return true;
            }

            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(string)));

            // if (context.Scanner.ReadText(Text, _comparer, null))
            // {
            //      success = true;
            //      value = Text;
            // }
            //
            // [if skipWhiteSpace]
            // if (!success)
            // {
            //      resetPosition(beginning);
            // }

            var ifReadText = Expression.IfThen(
                Expression.Call(
                    Expression.Field(context.ParseContext, "Scanner"),
                    ExpressionHelper.Scanner_ReadText_NoResult,
                    Expression.Constant(Text, typeof(string)),
                    Expression.Constant(_comparer, typeof(StringComparer))
                    ),
                Expression.Block(
                    Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                    context.DiscardResult
                    ? Expression.Empty()
                    : Expression.Assign(value, Expression.Constant(Text, typeof(string)))
                    )
                );

            result.Body.Add(ifReadText);

            return result;
        }
    }
}
