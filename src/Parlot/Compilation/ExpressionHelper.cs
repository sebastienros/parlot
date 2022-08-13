using Parlot.Fluent;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Globalization;

namespace Parlot.Compilation
{
    using System.Linq;

    public static class ExpressionHelper
    {
        public static Expression NewBufferSpan<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> _, Expression buffer, Expression offset, Expression count) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => Expression.New(ExpressionHelper<TParseContext, TChar>.BufferSpan_Constructor, new[] { buffer, offset, count });
        public static Expression SubBufferSpan<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context, Expression offset, Expression count) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => Expression.Call(context.Buffer(), ExpressionHelper<TParseContext, TChar>.BufferSpan_SubBuffer, new[] { offset, count });
        public static MemberExpression Scanner<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => Expression.Field(context.ParseContext, "Scanner");
        public static MemberExpression Cursor<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => Expression.Field(context.Scanner<TParseContext, TChar>(), "Cursor");
        public static MemberExpression Position<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => Expression.Property(context.Cursor<TParseContext, TChar>(), "Position");
        public static Expression ResetPosition<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context, Expression position) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => Expression.Call(context.Cursor(), typeof(Cursor<TChar>).GetMethod("ResetPosition"), position);
        public static MemberExpression Offset<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => Expression.Property(context.Cursor(), "Offset");
        public static MemberExpression Offset<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context, Expression textPosition) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => Expression.Field(textPosition, nameof(TextPosition.Offset));
        public static MemberExpression Current<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => Expression.Property(context.Cursor(), "Current");
        public static MemberExpression Eof<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => Expression.Property(context.Cursor(), "Eof");
        public static MemberExpression Buffer<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => Expression.Field(context.Scanner(), "Buffer");
        public static Expression ThrowObject<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> _, Expression o) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => Expression.Throw(Expression.New(typeof(Exception).GetConstructor(new[] { typeof(string) }), Expression.Call(o, o.Type.GetMethod("ToString", Type.EmptyTypes))));
        public static Expression ThrowParseException<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context, Expression message) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => Expression.Throw(Expression.New(typeof(ParseException).GetConstructors().First(), new[] { message, context.Position<TParseContext, TChar>() }));
        public static MethodCallExpression Advance<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => ExpressionHelper<TParseContext, TChar>.Advance(context);

        public static MethodCallExpression ReadSingleQuotedString<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_ReadSingleQuotedString, context.Scanner<TParseContext, char>());
        public static MethodCallExpression ReadDoubleQuotedString<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_ReadDoubleQuotedString, context.Scanner<TParseContext, char>());
        public static MethodCallExpression ReadQuotedString<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_ReadQuotedString, context.Scanner<TParseContext, char>());
        public static MethodCallExpression ReadChar<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context, TChar c) where TParseContext : ParseContextWithScanner<TChar> where TChar : IEquatable<TChar>, IConvertible => Expression.Call(context.Scanner(), ExpressionHelper<TParseContext, TChar>.Scanner_ReadChar, Expression.Constant(c));
        public static MethodCallExpression ReadDecimal<TParseContext>(this CompilationContext<TParseContext, char> context, NumberStyles style, CultureInfo culture) where TParseContext : ParseContextWithScanner<char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_ReadDecimal, context.Scanner<TParseContext, char>(), Expression.Constant(style), Expression.Constant(culture));
        public static MethodCallExpression ReadNonWhiteSpace<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_ReadNonWhiteSpace, context.Scanner<TParseContext, char>());
        public static MethodCallExpression ReadNonWhiteSpaceOrNewLine<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_ReadNonWhiteSpaceOrNewLine, context.Scanner<TParseContext, char>());
        public static MethodCallExpression SkipWhiteSpace<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_SkipWhiteSpace, context.Scanner<TParseContext, char>());
        public static MethodCallExpression SkipWhiteSpaceOrNewLine<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_SkipWhiteSpaceOrNewLine, context.Scanner<TParseContext, char>());

        public static ParameterExpression DeclareSuccessVariable<TParseContext>(this CompilationContext<TParseContext> context, CompilationResult result, bool defaultValue)
    where TParseContext : ParseContext
        {
            result.Success = Expression.Variable(typeof(bool), $"success{context.NextNumber}");
            result.Variables.Add(result.Success);
            result.Body.Add(Expression.Assign(result.Success, Expression.Constant(defaultValue, typeof(bool))));
            return result.Success;
        }

        public static ParameterExpression DeclareValueVariable<T, TParseContext>(this CompilationContext<TParseContext> context, CompilationResult result)
            where TParseContext : ParseContext
        {
            return DeclareValueVariable(context, result, Expression.Default(typeof(T)));
        }

        public static ParameterExpression DeclareValueVariable<TParseContext>(this CompilationContext<TParseContext> context, CompilationResult result, Expression defaultValue)
            where TParseContext : ParseContext
        {
            result.Value = Expression.Variable(defaultValue.Type, $"value{context.NextNumber}");

            if (!context.DiscardResult)
            {
                result.Variables.Add(result.Value);
                result.Body.Add(Expression.Assign(result.Value, defaultValue));
            }

            return result.Value;
        }

        public static ParameterExpression DeclarePositionVariable<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context, CompilationResult result)
    where TParseContext : ParseContextWithScanner<TChar>
    where TChar : IEquatable<TChar>, IConvertible
        {
            var start = Expression.Variable(typeof(TextPosition), $"position{context.NextNumber}");
            result.Variables.Add(start);
            result.Body.Add(Expression.Assign(start, context.Position()));
            return start;
        }

        public static ParameterExpression DeclareOffsetVariable<TParseContext, TChar>(this CompilationContext<TParseContext, TChar> context, CompilationResult result)
    where TParseContext : ParseContextWithScanner<TChar>
     where TChar : IEquatable<TChar>, IConvertible
        {
            var offset = Expression.Variable(typeof(int), $"offset{context.NextNumber}");
            result.Variables.Add(offset);
            result.Body.Add(Expression.Assign(offset, context.Offset()));
            return offset;
        }
    }

    public static class ExpressionHelper<TParseContext>
    where TParseContext : ParseContextWithScanner<char>
    {
        internal static MethodInfo Scanner_ReadText = typeof(CharScannerExtensions).GetMethod(nameof(Parlot.CharScannerExtensions.ReadText), new[] { typeof(Scanner<char>), typeof(string), typeof(StringComparer), typeof(TokenResult) });
        internal static MethodInfo Scanner_ReadText_NoResult = typeof(CharScannerExtensions).GetMethod(nameof(Parlot.CharScannerExtensions.ReadText), new[] { typeof(Scanner<char>), typeof(string), typeof(StringComparer) });
        internal static MethodInfo Scanner_ReadDecimal = typeof(CharScannerExtensions).GetMethod(nameof(Parlot.CharScannerExtensions.ReadDecimal), new Type[] { typeof(Scanner<char>), typeof(NumberStyles), typeof(CultureInfo) });
        internal static MethodInfo Scanner_ReadNonWhiteSpace = typeof(CharScannerExtensions).GetMethod(nameof(Parlot.CharScannerExtensions.ReadNonWhiteSpace), new Type[] { typeof(Scanner<char>) });
        internal static MethodInfo Scanner_ReadNonWhiteSpaceOrNewLine = typeof(CharScannerExtensions).GetMethod(nameof(Parlot.CharScannerExtensions.ReadNonWhiteSpaceOrNewLine), new Type[] { typeof(Scanner<char>) });
        internal static MethodInfo Scanner_SkipWhiteSpace = typeof(CharScannerExtensions).GetMethod(nameof(Parlot.CharScannerExtensions.SkipWhiteSpace), new Type[] { typeof(Scanner<char>) });
        internal static MethodInfo Scanner_SkipWhiteSpaceOrNewLine = typeof(CharScannerExtensions).GetMethod(nameof(Parlot.CharScannerExtensions.SkipWhiteSpaceOrNewLine), new Type[] { typeof(Scanner<char>) });
        internal static MethodInfo Scanner_ReadSingleQuotedString = typeof(CharScannerExtensions).GetMethod(nameof(Parlot.CharScannerExtensions.ReadSingleQuotedString), new Type[] { typeof(Scanner<char>) });
        internal static MethodInfo Scanner_ReadDoubleQuotedString = typeof(CharScannerExtensions).GetMethod(nameof(Parlot.CharScannerExtensions.ReadDoubleQuotedString), new Type[] { typeof(Scanner<char>) });
        internal static MethodInfo Scanner_ReadQuotedString = typeof(CharScannerExtensions).GetMethod(nameof(Parlot.CharScannerExtensions.ReadQuotedString), new Type[] { typeof(Scanner<char>) });


        // public static MethodCallExpression ReadSingleQuotedString(CompilationContext<TParseContext> context) => Expression.Call(ExpressionHelper<TParseContext, char>.Scanner(context), Scanner_ReadSingleQuotedString);
        // public static MethodCallExpression ReadDoubleQuotedString(CompilationContext<TParseContext> context) => Expression.Call(ExpressionHelper<TParseContext, char>.Scanner(context), Scanner_ReadDoubleQuotedString);
        // public static MethodCallExpression ReadQuotedString(CompilationContext<TParseContext> context) => Expression.Call(ExpressionHelper<TParseContext, char>.Scanner(context), Scanner_ReadQuotedString);
        // public static MethodCallExpression ReadChar(CompilationContext<TParseContext> context, char c) => Expression.Call(ExpressionHelper<TParseContext, char>.Scanner(context), Scanner_ReadChar, Expression.Constant(c));
        // public static MethodCallExpression ReadDecimal(CompilationContext<TParseContext> context) => Expression.Call(ExpressionHelper<TParseContext, char>.Scanner(context), Scanner_ReadDecimal);
        // public static MethodCallExpression ReadInteger(CompilationContext<TParseContext> context) => Expression.Call(ExpressionHelper<TParseContext, char>.Scanner(context), Scanner_ReadInteger);
        // public static MethodCallExpression ReadNonWhiteSpace(CompilationContext<TParseContext> context) => Expression.Call(ExpressionHelper<TParseContext, char>.Scanner(context), Scanner_ReadNonWhiteSpace);
        // public static MethodCallExpression ReadNonWhiteSpaceOrNewLine(CompilationContext<TParseContext> context) => Expression.Call(ExpressionHelper<TParseContext, char>.Scanner(context), Scanner_ReadNonWhiteSpaceOrNewLine);
        // public static MethodCallExpression SkipWhiteSpace(CompilationContext<TParseContext> context) => Expression.Call(ExpressionHelper<TParseContext, char>.Scanner(context), Scanner_SkipWhiteSpace);
        // public static MethodCallExpression SkipWhiteSpaceOrNewLine(CompilationContext<TParseContext> context) => Expression.Call(ExpressionHelper<TParseContext, char>.Scanner(context), Scanner_SkipWhiteSpaceOrNewLine);
    }

    public static class ExpressionHelper<TParseContext, TChar>
    where TParseContext : ParseContextWithScanner<TChar>
     where TChar : IEquatable<TChar>, IConvertible
    {
        internal static ConstructorInfo BufferSpan_Constructor = typeof(BufferSpan<TChar>).GetConstructor(new[] { typeof(TChar[]), typeof(int), typeof(int) });
        internal static MethodInfo BufferSpan_SubBuffer = typeof(BufferSpan<TChar>).GetMethod(nameof(BufferSpan<TChar>.SubBuffer), new[] { typeof(int), typeof(int) });
        internal static MethodInfo Scanner_ReadChar = typeof(Parlot.Scanner<TChar>).GetMethod(nameof(Parlot.Scanner<TChar>.ReadChar), new[] { typeof(TChar) });

        public static Expression NewBufferSpan(CompilationContext<TParseContext> _, Expression buffer, Expression offset, Expression count) => Expression.New(BufferSpan_Constructor, new[] { buffer, offset, count
    });
        public static MemberExpression Scanner(CompilationContext<TParseContext> context) => Expression.Field(context.ParseContext, "Scanner");
        public static MemberExpression Cursor(CompilationContext<TParseContext> context) => Expression.Field(Scanner(context), "Cursor");
        public static MemberExpression Position(CompilationContext<TParseContext> context) => Expression.Property(Cursor(context), "Position");
        public static Expression ResetPosition(CompilationContext<TParseContext> context, Expression position) => Expression.Call(Cursor(context), typeof(Cursor<TChar>).GetMethod("ResetPosition"), position);
        public static MemberExpression Offset(CompilationContext<TParseContext> context) => Expression.Property(Cursor(context), "Offset");
        public static MemberExpression Offset(CompilationContext<TParseContext> context, Expression textPosition) => Expression.Field(textPosition, nameof(TextPosition.Offset));
        public static MemberExpression Current(CompilationContext<TParseContext> context) => Expression.Property(Cursor(context), "Current");
        public static MemberExpression Eof(CompilationContext<TParseContext> context) => Expression.Property(Cursor(context), "Eof");
        public static MemberExpression Buffer(CompilationContext<TParseContext> context) => Expression.Field(Scanner(context), "Buffer");
        public static Expression ThrowObject(CompilationContext<TParseContext> _, Expression o) => Expression.Throw(Expression.New(typeof(Exception).GetConstructor(new[] { typeof(string) }), Expression.Call(o, o.Type.GetMethod("ToString", Type.EmptyTypes))));
        public static Expression ThrowParseException(CompilationContext<TParseContext> context, Expression message) => Expression.Throw(Expression.New(typeof(ParseException).GetConstructors().First(), new[] { message, Position(context) }));
        public static MethodCallExpression Advance(CompilationContext<TParseContext> context) => Expression.Call(Cursor(context), typeof(Cursor<TChar>).GetMethod("Advance", Type.EmptyTypes));

        public static ParameterExpression DeclareSuccessVariable(CompilationContext<TParseContext> context, CompilationResult result, bool defaultValue)
        {
            result.Success = Expression.Variable(typeof(bool), $"success{context.NextNumber}");
            result.Variables.Add(result.Success);
            result.Body.Add(Expression.Assign(result.Success, Expression.Constant(defaultValue, typeof(bool))));
            return result.Success;
        }

        public static ParameterExpression DeclareValueVariable<T2>(CompilationContext<TParseContext> context, CompilationResult result)
        {
            return DeclareValueVariable(context, result, Expression.Default(typeof(T2)));
        }

        public static ParameterExpression DeclareValueVariable(CompilationContext<TParseContext> context, CompilationResult result, Expression defaultValue)
        {
            result.Value = Expression.Variable(defaultValue.Type, $"value{context.NextNumber}");

            if (!context.DiscardResult)
            {
                result.Variables.Add(result.Value);
                result.Body.Add(Expression.Assign(result.Value, defaultValue));
            }

            return result.Value;
        }

        public static ParameterExpression DeclarePositionVariable(CompilationContext<TParseContext> context, CompilationResult result)
        {
            var start = Expression.Variable(typeof(TextPosition), $"position{context.NextNumber}");
            result.Variables.Add(start);
            result.Body.Add(Expression.Assign(start, Position(context)));
            return start;
        }

        public static ParameterExpression DeclareOffsetVariable(CompilationContext<TParseContext> context, CompilationResult result)
        {
            var offset = Expression.Variable(typeof(int), $"offset{context.NextNumber}");
            result.Variables.Add(offset);
            result.Body.Add(Expression.Assign(offset, Offset(context)));
            return offset;
        }
    }
}
