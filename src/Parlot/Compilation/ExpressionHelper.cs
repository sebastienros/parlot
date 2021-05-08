using Parlot.Fluent;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Compilation
{
    using System.Linq;

    public static class ExpressionHelper
    {
        public static Expression NewBufferSpan<TParseContext, T>(this CompilationContext<TParseContext, T> _, Expression buffer, Expression offset, Expression count) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => Expression.New(ExpressionHelper<TParseContext, T>.BufferSpan_Constructor, new[] { buffer, offset, count });
        public static Expression SubBufferSpan<TParseContext, T>(this CompilationContext<TParseContext, T> context, Expression offset, Expression count) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => Expression.Call(context.Buffer(), ExpressionHelper<TParseContext, T>.BufferSpan_SubBuffer, new[] { offset, count });
        public static MemberExpression Scanner<TParseContext, T>(this CompilationContext<TParseContext, T> context) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => Expression.Field(context.ParseContext, "Scanner");
        public static MemberExpression Cursor<TParseContext, T>(this CompilationContext<TParseContext, T> context) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => Expression.Field(context.Scanner<TParseContext, T>(), "Cursor");
        public static MemberExpression Position<TParseContext, T>(this CompilationContext<TParseContext, T> context) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => Expression.Property(context.Cursor<TParseContext, T>(), "Position");
        public static Expression ResetPosition<TParseContext, T>(this CompilationContext<TParseContext, T> context, Expression position) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => Expression.Call(context.Cursor(), typeof(Cursor<T>).GetMethod("ResetPosition"), position);
        public static MemberExpression Offset<TParseContext, T>(this CompilationContext<TParseContext, T> context) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => Expression.Property(context.Cursor(), "Offset");
        public static MemberExpression Offset<TParseContext, T>(this CompilationContext<TParseContext, T> context, Expression textPosition) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => Expression.Field(textPosition, nameof(TextPosition.Offset));
        public static MemberExpression Current<TParseContext, T>(this CompilationContext<TParseContext, T> context) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => Expression.Property(context.Cursor(), "Current");
        public static MemberExpression Eof<TParseContext, T>(this CompilationContext<TParseContext, T> context) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => Expression.Property(context.Cursor(), "Eof");
        public static MemberExpression Buffer<TParseContext, T>(this CompilationContext<TParseContext, T> context) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => Expression.Field(context.Scanner(), "Buffer");
        public static Expression ThrowObject<TParseContext, T>(this CompilationContext<TParseContext, T> _, Expression o) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => Expression.Throw(Expression.New(typeof(Exception).GetConstructor(new[] { typeof(string) }), Expression.Call(o, o.Type.GetMethod("ToString", Type.EmptyTypes))));
        public static Expression ThrowParseException<TParseContext, T>(this CompilationContext<TParseContext, T> context, Expression message) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => Expression.Throw(Expression.New(typeof(ParseException).GetConstructors().First(), new[] { message, context.Position<TParseContext, T>() }));
        public static MethodCallExpression Advance<TParseContext, T>(this CompilationContext<TParseContext, T> context) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => ExpressionHelper<TParseContext, T>.Advance(context);

        public static MethodCallExpression ReadSingleQuotedString<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<Scanner<char>, char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_ReadSingleQuotedString, context.Scanner<TParseContext, char>());
        public static MethodCallExpression ReadDoubleQuotedString<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<Scanner<char>, char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_ReadDoubleQuotedString, context.Scanner<TParseContext, char>());
        public static MethodCallExpression ReadQuotedString<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<Scanner<char>, char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_ReadQuotedString, context.Scanner<TParseContext, char>());
        public static MethodCallExpression ReadChar<TParseContext, T>(this CompilationContext<TParseContext, T> context, T c) where TParseContext : ParseContextWithScanner<Scanner<T>, T> where T : IEquatable<T>, IConvertible => Expression.Call(context.Scanner(), ExpressionHelper<TParseContext, T>.Scanner_ReadChar, Expression.Constant(c));
        public static MethodCallExpression ReadDecimal<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<Scanner<char>, char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_ReadDecimal, context.Scanner<TParseContext, char>());
        public static MethodCallExpression ReadInteger<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<Scanner<char>, char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_ReadInteger, context.Scanner<TParseContext, char>());
        public static MethodCallExpression ReadNonWhiteSpace<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<Scanner<char>, char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_ReadNonWhiteSpace, context.Scanner<TParseContext, char>());
        public static MethodCallExpression ReadNonWhiteSpaceOrNewLine<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<Scanner<char>, char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_ReadNonWhiteSpaceOrNewLine, context.Scanner<TParseContext, char>());
        public static MethodCallExpression SkipWhiteSpace<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<Scanner<char>, char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_SkipWhiteSpace, context.Scanner<TParseContext, char>());
        public static MethodCallExpression SkipWhiteSpaceOrNewLine<TParseContext>(this CompilationContext<TParseContext, char> context) where TParseContext : ParseContextWithScanner<Scanner<char>, char> => Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_SkipWhiteSpaceOrNewLine, context.Scanner<TParseContext, char>());

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

        public static ParameterExpression DeclarePositionVariable<TParseContext, T>(this CompilationContext<TParseContext, T> context, CompilationResult result)
    where TParseContext : ParseContextWithScanner<Scanner<T>, T>
    where T : IEquatable<T>, IConvertible
        {
            var start = Expression.Variable(typeof(TextPosition), $"position{context.NextNumber}");
            result.Variables.Add(start);
            result.Body.Add(Expression.Assign(start, context.Position()));
            return start;
        }

        public static ParameterExpression DeclareOffsetVariable<TParseContext, T>(this CompilationContext<TParseContext, T> context, CompilationResult result)
    where TParseContext : ParseContextWithScanner<Scanner<T>, T>
     where T : IEquatable<T>, IConvertible
        {
            var offset = Expression.Variable(typeof(int), $"offset{context.NextNumber}");
            result.Variables.Add(offset);
            result.Body.Add(Expression.Assign(offset, context.Offset()));
            return offset;
        }

        public static MethodCallExpression ParserSkipWhiteSpace<TParseContext>(this CompilationContext<TParseContext> context)
    where TParseContext : ParseContext
        {
            if (context is StringParseContext stringParseContext)
                return Expression.Call(context.ParseContext, ExpressionHelper.ParserContext_SkipWhiteSpaceMethod);
            else if (context is ParseContextWithScanner<Scanner<char>, char> charContext)
                charContext.Scanner.SkipWhiteSpace();
            return Expression.Call(context.ParseContext, ExpressionHelper.ParserContext_SkipWhiteSpaceMethod);
        }
        public static MethodCallExpression ParserSkipWhiteSpace<TParseContext>(this CompilationContext<TParseContext, char> context)
    where TParseContext : ParseContextWithScanner<Scanner<char>, char>
        {
            if (typeof(TParseContext).IsAssignableFrom(typeof(StringParseContext)))
                return Expression.Call(context.ParseContext, ExpressionHelper.ParserContext_SkipWhiteSpaceMethod);
            else
                return Expression.Call(null, ExpressionHelper<TParseContext>.Scanner_SkipWhiteSpace, context.Scanner());
        }

        internal static MethodInfo ParserContext_SkipWhiteSpaceMethod = typeof(StringParseContext).GetMethod(nameof(StringParseContext.SkipWhiteSpace), Array.Empty<Type>());

    }

    public static class ExpressionHelper<TParseContext>
    where TParseContext : ParseContextWithScanner<Scanner<char>, char>
    {
        internal static MethodInfo Scanner_ReadText = typeof(CharScannerExtensions).GetMethod(nameof(Parlot.CharScannerExtensions.ReadText), new[] { typeof(Scanner<char>), typeof(string), typeof(StringComparer), typeof(TokenResult) });
        internal static MethodInfo Scanner_ReadText_NoResult = typeof(CharScannerExtensions).GetMethod(nameof(Parlot.CharScannerExtensions.ReadText), new[] { typeof(Scanner<char>), typeof(string), typeof(StringComparer) });
        internal static MethodInfo Scanner_ReadDecimal = typeof(CharScannerExtensions).GetMethod(nameof(Parlot.CharScannerExtensions.ReadDecimal), new Type[] { typeof(Scanner<char>) });
        internal static MethodInfo Scanner_ReadInteger = typeof(CharScannerExtensions).GetMethod(nameof(Parlot.CharScannerExtensions.ReadInteger), new Type[] { typeof(Scanner<char>) });
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

        public static MethodCallExpression ParserSkipWhiteSpace(CompilationContext<TParseContext> context)
        {
            return Expression.Call(context.ParseContext, ExpressionHelper.ParserContext_SkipWhiteSpaceMethod);
        }
    }

    public static class ExpressionHelper<TParseContext, T>
    where TParseContext : ParseContextWithScanner<Scanner<T>, T>
     where T : IEquatable<T>, IConvertible
    {
        internal static ConstructorInfo BufferSpan_Constructor = typeof(BufferSpan<T>).GetConstructor(new[] { typeof(T[]), typeof(int), typeof(int) });
        internal static MethodInfo BufferSpan_SubBuffer = typeof(BufferSpan<T>).GetMethod(nameof(BufferSpan<T>.SubBuffer), new[] { typeof(int), typeof(int) });
        internal static MethodInfo Scanner_ReadChar = typeof(Parlot.Scanner<T>).GetMethod(nameof(Parlot.Scanner<T>.ReadChar), new[] { typeof(T) });

        public static Expression NewBufferSpan(CompilationContext<TParseContext> _, Expression buffer, Expression offset, Expression count) => Expression.New(BufferSpan_Constructor, new[] { buffer, offset, count
    });
        public static MemberExpression Scanner(CompilationContext<TParseContext> context) => Expression.Field(context.ParseContext, "Scanner");
        public static MemberExpression Cursor(CompilationContext<TParseContext> context) => Expression.Field(Scanner(context), "Cursor");
        public static MemberExpression Position(CompilationContext<TParseContext> context) => Expression.Property(Cursor(context), "Position");
        public static Expression ResetPosition(CompilationContext<TParseContext> context, Expression position) => Expression.Call(Cursor(context), typeof(Cursor<T>).GetMethod("ResetPosition"), position);
        public static MemberExpression Offset(CompilationContext<TParseContext> context) => Expression.Property(Cursor(context), "Offset");
        public static MemberExpression Offset(CompilationContext<TParseContext> context, Expression textPosition) => Expression.Field(textPosition, nameof(TextPosition.Offset));
        public static MemberExpression Current(CompilationContext<TParseContext> context) => Expression.Property(Cursor(context), "Current");
        public static MemberExpression Eof(CompilationContext<TParseContext> context) => Expression.Property(Cursor(context), "Eof");
        public static MemberExpression Buffer(CompilationContext<TParseContext> context) => Expression.Field(Scanner(context), "Buffer");
        public static Expression ThrowObject(CompilationContext<TParseContext> _, Expression o) => Expression.Throw(Expression.New(typeof(Exception).GetConstructor(new[] { typeof(string) }), Expression.Call(o, o.Type.GetMethod("ToString", Type.EmptyTypes))));
        public static Expression ThrowParseException(CompilationContext<TParseContext> context, Expression message) => Expression.Throw(Expression.New(typeof(ParseException).GetConstructors().First(), new[] { message, Position(context) }));
        public static MethodCallExpression Advance(CompilationContext<TParseContext> context) => Expression.Call(Cursor(context), typeof(Cursor<T>).GetMethod("Advance", Type.EmptyTypes));

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
