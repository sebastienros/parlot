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

        internal static Expression<Func<ParseContext, int>> GetOffsetExpression() => (context) => context.Scanner.Cursor.Offset;
    }
}
