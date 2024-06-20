using Parlot.Fluent;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Compilation
{
    using System.Linq;
    using System.Xml;

    public static class ExpressionHelper
    {
        internal static readonly MethodInfo ParserContext_SkipWhiteSpaceMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.SkipWhiteSpace), Array.Empty<Type>())!;
        internal static readonly MethodInfo ParserContext_WhiteSpaceParser = typeof(ParseContext).GetProperty(nameof(ParseContext.WhiteSpaceParser))?.GetGetMethod()!;
        internal static readonly MethodInfo Scanner_ReadText_NoResult = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadText), [typeof(ReadOnlySpan<char>), typeof(StringComparison)])!;
        internal static readonly MethodInfo Scanner_ReadChar = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadChar), [typeof(char)])!;
        internal static readonly MethodInfo Scanner_ReadDecimal = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadDecimal), [])!;
        internal static readonly MethodInfo Scanner_ReadDecimalAllArguments = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadDecimal), [typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(ReadOnlySpan<char>).MakeByRefType(), typeof(char), typeof(char)])!;
        internal static readonly MethodInfo Scanner_ReadInteger = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadInteger), [])!;
        internal static readonly MethodInfo Scanner_ReadNonWhiteSpace = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadNonWhiteSpace), [])!;
        internal static readonly MethodInfo Scanner_ReadNonWhiteSpaceOrNewLine = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadNonWhiteSpaceOrNewLine), [])!;
        internal static readonly MethodInfo Scanner_SkipWhiteSpace = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.SkipWhiteSpace), [])!;
        internal static readonly MethodInfo Scanner_SkipWhiteSpaceOrNewLine = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.SkipWhiteSpaceOrNewLine), [])!;
        internal static readonly MethodInfo Scanner_ReadSingleQuotedString = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadSingleQuotedString), [])!;
        internal static readonly MethodInfo Scanner_ReadDoubleQuotedString = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadDoubleQuotedString), [])!;
        internal static readonly MethodInfo Scanner_ReadQuotedString = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadQuotedString), [])!;

        internal static readonly MethodInfo Cursor_Advance = typeof(Cursor).GetMethod(nameof(Parlot.Cursor.Advance), Array.Empty<Type>())!;
        internal static readonly MethodInfo Cursor_AdvanceNoNewLines = typeof(Cursor).GetMethod(nameof(Parlot.Cursor.AdvanceNoNewLines), [typeof(int)])!;
        internal static readonly MethodInfo Cursor_ResetPosition = typeof(Cursor).GetMethod("ResetPosition")!;

        internal static readonly ConstructorInfo Exception_ToString = typeof(Exception).GetConstructor([typeof(string)])!;

        internal static ConstructorInfo TextSpan_Constructor = typeof(TextSpan).GetConstructor([typeof(string), typeof(int), typeof(int)])!;

        internal static MethodInfo MemoryExtensions_AsSpan = typeof(MemoryExtensions).GetMethod(nameof(MemoryExtensions.AsSpan), [typeof(string)])!;

        public static Expression ArrayEmpty<T>() => ((Expression<Func<object>>)(() => Array.Empty<T>())).Body;
        public static Expression New<T>() where T : new() => ((Expression<Func<T>>)(() => new T())).Body;
        
        public static Expression<Func<Cursor, char, char, bool>> CharacterIsInRange = (cursor,b,c) => Character.IsInRange(cursor.Current, b, c);

        //public static Expression NewOptionalResult<T>(this CompilationContext _, Expression hasValue, Expression value) => Expression.New(GetOptionalResult_Constructor<T>(), [hasValue, value]);
        public static Expression NewTextSpan(this CompilationContext _, Expression buffer, Expression offset, Expression count) => Expression.New(TextSpan_Constructor, [buffer, offset, count]);
        public static MemberExpression Scanner(this CompilationContext context) => Expression.Field(context.ParseContext, "Scanner");
        public static MemberExpression Cursor(this CompilationContext context) => Expression.Field(context.Scanner(), "Cursor");
        public static MemberExpression Position(this CompilationContext context) => Expression.Property(context.Cursor(), "Position");
        public static Expression ResetPosition(this CompilationContext context, Expression position) => Expression.Call(context.Cursor(), Cursor_ResetPosition, position);
        public static MemberExpression Offset(this CompilationContext context) => Expression.Property(context.Cursor(), "Offset");
        public static MemberExpression Offset(this CompilationContext _, Expression textPosition) => Expression.Field(textPosition, nameof(TextPosition.Offset));
        public static MemberExpression Current(this CompilationContext context) => Expression.Property(context.Cursor(), "Current");
        public static MemberExpression Eof(this CompilationContext context) => Expression.Property(context.Cursor(), "Eof");
        public static MemberExpression Buffer(this CompilationContext context) => Expression.Field(context.Scanner(), "Buffer");
        public static Expression ThrowObject(this CompilationContext _, Expression o) => Expression.Throw(Expression.New(Exception_ToString, Expression.Call(o, o.Type.GetMethod("ToString", [])!)));
        public static Expression ThrowParseException(this CompilationContext context, Expression message) => Expression.Throw(Expression.New(typeof(ParseException).GetConstructors().First(), [message, context.Position()] ));

        public static MethodCallExpression ReadSingleQuotedString(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_ReadSingleQuotedString);
        public static MethodCallExpression ReadDoubleQuotedString(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_ReadDoubleQuotedString);
        public static MethodCallExpression ReadQuotedString(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_ReadQuotedString);
        public static MethodCallExpression ReadChar(this CompilationContext context, char c) => Expression.Call(context.Scanner(), Scanner_ReadChar, Expression.Constant(c));
        public static MethodCallExpression ReadDecimal(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_ReadDecimal);
        public static MethodCallExpression ReadDecimal(this CompilationContext context, Expression allowLeadingSign, Expression allowDecimalSeparator, Expression allowGroupSeparator, Expression allowExponent, Expression number, Expression decimalSeparator, Expression groupSeparator) => Expression.Call(context.Scanner(), Scanner_ReadDecimalAllArguments, allowLeadingSign, allowDecimalSeparator, allowGroupSeparator, allowExponent, number, decimalSeparator, groupSeparator);
        public static MethodCallExpression ReadInteger(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_ReadInteger);
        public static MethodCallExpression ReadNonWhiteSpace(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_ReadNonWhiteSpace);
        public static MethodCallExpression ReadNonWhiteSpaceOrNewLine(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_ReadNonWhiteSpaceOrNewLine);
        public static MethodCallExpression SkipWhiteSpace(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_SkipWhiteSpace);
        public static MethodCallExpression SkipWhiteSpaceOrNewLine(this CompilationContext context) => Expression.Call(context.Scanner(), Scanner_SkipWhiteSpaceOrNewLine);
        public static MethodCallExpression Advance(this CompilationContext context) => Expression.Call(context.Cursor(), Cursor_Advance);
        public static MethodCallExpression AdvanceNoNewLine(this CompilationContext context, Expression count) => Expression.Call(context.Cursor(), Cursor_AdvanceNoNewLines, [count]);

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
