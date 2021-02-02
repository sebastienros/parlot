namespace Parlot.Fluent
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text;

    internal static class ExpressionHelper
    {
        internal static MethodInfo ParserContext_SkipWhiteSpaceMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.SkipWhiteSpace), Array.Empty<Type>());
        internal static MethodInfo Scanner_ReadText = typeof(Scanner).GetMethod(nameof(Scanner.ReadText), new[] { typeof(string), typeof(StringComparer), typeof(TokenResult) });

        //internal static Expression< ParserContext_ScannerProperty = typeof(ParseContext).GetMethod(nameof(ParseContext.SkipWhiteSpace), Array.Empty<Type>());

        internal static Expression ReadChar(Expression parseContext, char c) => Expression.Call(Expression.Field(parseContext, "Scanner"), typeof(Scanner).GetMethod(nameof(Scanner.ReadChar)), Expression.Constant(c), Expression.Constant(null, typeof(TokenResult)));
        internal static Expression ReadDecimal(Expression parseContext) => Expression.Call(Expression.Field(parseContext, "Scanner"), typeof(Scanner).GetMethod(nameof(Scanner.ReadDecimal)), Expression.Constant(null, typeof(TokenResult)));
        internal static Expression Offset(Expression parseContext) => Expression.Property(Expression.Field(Expression.Field(parseContext, "Scanner"), "Cursor"), "Offset");
        internal static Expression Eof(Expression parseContext) => Expression.Property(Expression.Field(Expression.Field(parseContext, "Scanner"), "Cursor"), "Eof");
        internal static Expression Buffer(Expression parseContext) => Expression.Field(Expression.Field(parseContext, "Scanner"), "Buffer");
    }
}
