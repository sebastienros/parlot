﻿using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class TextLiteral : Parser<string>, ICompilable, ISeekable
    {
        private readonly StringComparison _comparisonType;

        public TextLiteral(string text, StringComparison comparisonType)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            _comparisonType = comparisonType;
        }

        public string Text { get; }

        public bool CanSeek => Text.Length > 0;

        public char[] ExpectedChars => new[] { Text[0] };

        public bool SkipWhitespace => false;

        public override bool Parse(ParseContext context, ref ParseResult<string> result)
        {
            context.EnterParser(this);

            var cursor = context.Scanner.Cursor;

            if (cursor.Match(Text, _comparisonType))
            {
                var start = cursor.Offset;
                cursor.Advance(Text.Length);
                result.Set(start, cursor.Offset, Text);
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
                    Expression.Constant(_comparisonType, typeof(StringComparison))
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
