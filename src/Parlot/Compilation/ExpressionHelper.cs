using Parlot.Fluent;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Compilation
{
    internal static class ExpressionHelper
    {
        internal static MethodInfo ParserContext_SkipWhiteSpaceMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.SkipWhiteSpace), Array.Empty<Type>());
        internal static MethodInfo Scanner_ReadText = typeof(Scanner).GetMethod(nameof(Scanner.ReadText), new[] { typeof(string), typeof(StringComparer), typeof(TokenResult) });
        internal static MethodInfo Scanner_ReadText_NoResult = typeof(Scanner).GetMethod(nameof(Scanner.ReadText), new[] { typeof(string), typeof(StringComparer) });

        //internal static Expression< ParserContext_ScannerProperty = typeof(ParseContext).GetMethod(nameof(ParseContext.SkipWhiteSpace), Array.Empty<Type>());

        internal static Expression ReadChar(Expression parseContext, char c) => Expression.Call(Expression.Field(parseContext, "Scanner"), typeof(Scanner).GetMethod(nameof(Scanner.ReadChar), new[] { typeof(char) }), Expression.Constant(c));
        internal static Expression ReadDecimal(Expression parseContext) => Expression.Call(Expression.Field(parseContext, "Scanner"), typeof(Scanner).GetMethod(nameof(Scanner.ReadDecimal), new Type[0] { }));
        internal static Expression Advance(Expression parseContext) => Expression.Call(Expression.Field(Expression.Field(parseContext, "Scanner"), "Cursor"), typeof(Cursor).GetMethod(nameof(Cursor.Advance), new Type[0] { }));
        internal static Expression Position(Expression parseContext) => Expression.Property(Expression.Field(Expression.Field(parseContext, "Scanner"), "Cursor"), "Position");
        internal static Expression ResetPosition(Expression parseContext, Expression position) => Expression.Call(Expression.Field(Expression.Field(parseContext, "Scanner"), "Cursor"), typeof(Cursor).GetMethod("ResetPosition"), position);
        internal static Expression Offset(Expression parseContext) => Expression.Property(Expression.Field(Expression.Field(parseContext, "Scanner"), "Cursor"), "Offset");
        internal static Expression Current(Expression parseContext) => Expression.Property(Expression.Field(Expression.Field(parseContext, "Scanner"), "Cursor"), "Current");
        internal static Expression Eof(Expression parseContext) => Expression.Property(Expression.Field(Expression.Field(parseContext, "Scanner"), "Cursor"), "Eof");
        internal static Expression Buffer(Expression parseContext) => Expression.Field(Expression.Field(parseContext, "Scanner"), "Buffer");
    }
}
