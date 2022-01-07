using Parlot.Fluent;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Compilation
{
    using System.Linq;

    public static class ExpressionHelper
    {
        internal static MethodInfo ParserContext_SkipWhiteSpaceMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.SkipWhiteSpace), Array.Empty<Type>());
        internal static MethodInfo Scanner_ReadText = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadText), new[] { typeof(string), typeof(StringComparison), typeof(TokenResult) });
        internal static MethodInfo Scanner_ReadText_NoResult = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadText), new[] { typeof(string), typeof(StringComparison) });
        internal static MethodInfo Scanner_ReadChar = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadChar), new[] { typeof(char) });
        internal static MethodInfo Scanner_ReadDecimal = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadDecimal), new Type[0] { });
        internal static MethodInfo Scanner_ReadInteger = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadInteger), new Type[0] { });
        internal static MethodInfo Scanner_ReadNonWhiteSpace = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadNonWhiteSpace), new Type[0] { });
        internal static MethodInfo Scanner_ReadNonWhiteSpaceOrNewLine = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadNonWhiteSpaceOrNewLine), new Type[0] { });
        internal static MethodInfo Scanner_SkipWhiteSpace = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.SkipWhiteSpace), new Type[0] { });
        internal static MethodInfo Scanner_SkipWhiteSpaceOrNewLine = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.SkipWhiteSpaceOrNewLine), new Type[0] { });
        internal static MethodInfo Scanner_ReadSingleQuotedString = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadSingleQuotedString), new Type[0] { });
        internal static MethodInfo Scanner_ReadDoubleQuotedString = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadDoubleQuotedString), new Type[0] { });
        internal static MethodInfo Scanner_ReadQuotedString = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadQuotedString), new Type[0] { });

        internal static MethodInfo Cursor_Advance = typeof(Cursor).GetMethod(nameof(Parlot.Cursor.Advance), Array.Empty<Type>());
        internal static MethodInfo Cursor_AdvanceNoNewLines = typeof(Cursor).GetMethod(nameof(Parlot.Cursor.AdvanceNoNewLines), new Type[] { typeof(int) });

        internal static ConstructorInfo TextSpan_Constructor = typeof(TextSpan).GetConstructor(new[] { typeof(string), typeof(int), typeof(int) });

        public static Expression NewTextSpan(this CompilationContext _, Expression buffer, Expression offset, Expression count) => Expression.New(TextSpan_Constructor, new[] { buffer, offset, count });
        public static MemberExpression Scanner(this CompilationContext context) => Expression.Field(context.ParseContext, "Scanner");
        public static MemberExpression Cursor(this CompilationContext context) => Expression.Field(context.Scanner(), "Cursor");
        public static MemberExpression Position(this CompilationContext context) => Expression.Property(context.Cursor(), "Position");
        public static Expression ResetPosition(this CompilationContext context, Expression position) => Expression.Call(context.Cursor(), typeof(Cursor).GetMethod("ResetPosition"), position);
        public static MemberExpression Offset(this CompilationContext context) => Expression.Property(context.Cursor(), "Offset");
        public static MemberExpression Offset(this CompilationContext context, Expression textPosition) => Expression.Field(textPosition, nameof(TextPosition.Offset));
        public static MemberExpression Current(this CompilationContext context) => Expression.Property(context.Cursor(), "Current");
        public static MemberExpression Eof(this CompilationContext context) => Expression.Property(context.Cursor(), "Eof");
        public static MemberExpression Buffer(this CompilationContext context) => Expression.Field(context.Scanner(), "Buffer");
        public static Expression ThrowObject(this CompilationContext _, Expression o) => Expression.Throw(Expression.New(typeof(Exception).GetConstructor(new[] { typeof(string) }), Expression.Call(o, o.Type.GetMethod("ToString", new Type[0]))));
        public static Expression ThrowParseException(this CompilationContext context, Expression message) => Expression.Throw(Expression.New(typeof(ParseException).GetConstructors().First(), new [] { message, context.Position() } ));

        public static MethodCallExpression ReadSingleQuotedString(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_ReadSingleQuotedString);
        public static MethodCallExpression ReadDoubleQuotedString(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_ReadDoubleQuotedString);
        public static MethodCallExpression ReadQuotedString(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_ReadQuotedString);
        public static MethodCallExpression ReadChar(this CompilationContext context, char c) => Expression.Call(context.Scanner(), Scanner_ReadChar, Expression.Constant(c));
        public static MethodCallExpression ReadDecimal(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_ReadDecimal);
        public static MethodCallExpression ReadInteger(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_ReadInteger);
        public static MethodCallExpression ReadNonWhiteSpace(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_ReadNonWhiteSpace);
        public static MethodCallExpression ReadNonWhiteSpaceOrNewLine(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_ReadNonWhiteSpaceOrNewLine);
        public static MethodCallExpression SkipWhiteSpace(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_SkipWhiteSpace);
        public static MethodCallExpression SkipWhiteSpaceOrNewLine(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_SkipWhiteSpaceOrNewLine);
        public static MethodCallExpression Advance(this CompilationContext context) => Expression.Call(context.Cursor(), Cursor_Advance);
        public static MethodCallExpression AdvanceNoNewLine(this CompilationContext context, Expression count) => Expression.Call(context.Cursor(), Cursor_AdvanceNoNewLines, new[] { count });

        public static ParameterExpression DeclareSuccessVariable(this CompilationContext context, CompilationResult result, bool defaultValue)
        {
            result.Success = Expression.Variable(typeof(bool), $"success{context.NextNumber}");
            result.Variables.Add(result.Success);
            result.Body.Add(Expression.Assign(result.Success, Expression.Constant(defaultValue, typeof(bool))));
            return result.Success;
        }

        public static ParameterExpression DeclareValueVariable<T>(this CompilationContext context, CompilationResult result)
        {
            return DeclareValueVariable(context, result, Expression.Default(typeof(T)));
        }
            
        public static ParameterExpression DeclareValueVariable(this CompilationContext context, CompilationResult result, Expression defaultValue)
        {
            result.Value = Expression.Variable(defaultValue.Type, $"value{context.NextNumber}");

            if (!context.DiscardResult)
            {
                result.Variables.Add(result.Value);
                result.Body.Add(Expression.Assign(result.Value, defaultValue));
            }

            return result.Value;
        }

        public static ParameterExpression DeclarePositionVariable(this CompilationContext context, CompilationResult result)
        {
            var start = Expression.Variable(typeof(TextPosition), $"position{context.NextNumber}");
            result.Variables.Add(start);
            result.Body.Add(Expression.Assign(start, context.Position()));
            return start;
        }

        public static ParameterExpression DeclareOffsetVariable(this CompilationContext context, CompilationResult result)
        {
            var offset = Expression.Variable(typeof(int), $"offset{context.NextNumber}");
            result.Variables.Add(offset);
            result.Body.Add(Expression.Assign(offset, context.Offset()));
            return offset;
        }

        public static MethodCallExpression ParserSkipWhiteSpace(this CompilationContext context)
        {
            return Expression.Call(context.ParseContext, ParserContext_SkipWhiteSpaceMethod);
        }
    }
}
