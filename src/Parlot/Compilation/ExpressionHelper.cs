using Parlot.Fluent;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Compilation
{
    using System.Linq;

    public static class ExpressionHelper
    {
        public static Expression NewTextSpan<TParseContext>(this CompilationContext<TParseContext> _, Expression buffer, Expression offset, Expression count) where TParseContext : ParseContext => Expression.New(ExpressionHelper<TParseContext>.TextSpan_Constructor, new[] { buffer, offset, count });
        public static MemberExpression Scanner<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Field(context.ParseContext, "Scanner");
        public static MemberExpression Cursor<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Field(context.Scanner(), "Cursor");
        public static MemberExpression Position<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Property(context.Cursor(), "Position");
        public static Expression ResetPosition<TParseContext>(this CompilationContext<TParseContext> context, Expression position) where TParseContext : ParseContext => Expression.Call(context.Cursor(), typeof(Cursor).GetMethod("ResetPosition"), position);
        public static MemberExpression Offset<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Property(context.Cursor(), "Offset");
        public static MemberExpression Offset<TParseContext>(this CompilationContext<TParseContext> context, Expression textPosition) where TParseContext : ParseContext => Expression.Field(textPosition, nameof(TextPosition.Offset));
        public static MemberExpression Current<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Property(context.Cursor(), "Current");
        public static MemberExpression Eof<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Property(context.Cursor(), "Eof");
        public static MemberExpression Buffer<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Field(context.Scanner(), "Buffer");
        public static Expression ThrowObject<TParseContext>(this CompilationContext<TParseContext> _, Expression o) where TParseContext : ParseContext => Expression.Throw(Expression.New(typeof(Exception).GetConstructor(new[] { typeof(string) }), Expression.Call(o, o.Type.GetMethod("ToString", new Type[0]))));
        public static Expression ThrowParseException<TParseContext>(this CompilationContext<TParseContext> context, Expression message) where TParseContext : ParseContext => Expression.Throw(Expression.New(typeof(ParseException).GetConstructors().First(), new [] { message, context.Position() } ));

        public static MethodCallExpression ReadSingleQuotedString<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Call(context.Scanner(), ExpressionHelper<TParseContext>.Scanner_ReadSingleQuotedString);
        public static MethodCallExpression ReadDoubleQuotedString<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Call(context.Scanner(), ExpressionHelper<TParseContext>.Scanner_ReadDoubleQuotedString);
        public static MethodCallExpression ReadQuotedString<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Call(context.Scanner(), ExpressionHelper<TParseContext>.Scanner_ReadQuotedString);
        public static MethodCallExpression ReadChar<TParseContext>(this CompilationContext<TParseContext> context, char c) where TParseContext : ParseContext => Expression.Call(context.Scanner(), ExpressionHelper<TParseContext>.Scanner_ReadChar, Expression.Constant(c));
        public static MethodCallExpression ReadDecimal<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Call(context.Scanner(), ExpressionHelper<TParseContext>.Scanner_ReadDecimal);
        public static MethodCallExpression ReadInteger<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Call(context.Scanner(), ExpressionHelper<TParseContext>.Scanner_ReadInteger);
        public static MethodCallExpression ReadNonWhiteSpace<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Call(context.Scanner(), ExpressionHelper<TParseContext>.Scanner_ReadNonWhiteSpace);
        public static MethodCallExpression ReadNonWhiteSpaceOrNewLine<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Call(context.Scanner(), ExpressionHelper<TParseContext>.Scanner_ReadNonWhiteSpaceOrNewLine);
        public static MethodCallExpression SkipWhiteSpace<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Call(context.Scanner(), ExpressionHelper<TParseContext>.Scanner_SkipWhiteSpace);
        public static MethodCallExpression SkipWhiteSpaceOrNewLine<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Call(context.Scanner(), ExpressionHelper<TParseContext>.Scanner_SkipWhiteSpaceOrNewLine);
        public static MethodCallExpression Advance<TParseContext>(this CompilationContext<TParseContext> context) where TParseContext : ParseContext => Expression.Call(context.Cursor(), typeof(Cursor).GetMethod(nameof(Parlot.Cursor.Advance), new Type[0] { }));

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

        public static ParameterExpression DeclarePositionVariable<TParseContext>(this CompilationContext<TParseContext> context, CompilationResult result)
    where TParseContext : ParseContext
        {
            var start = Expression.Variable(typeof(TextPosition), $"position{context.NextNumber}");
            result.Variables.Add(start);
            result.Body.Add(Expression.Assign(start, context.Position()));
            return start;
        }

        public static ParameterExpression DeclareOffsetVariable<TParseContext>(this CompilationContext<TParseContext> context, CompilationResult result)
    where TParseContext : ParseContext
        {
            var offset = Expression.Variable(typeof(int), $"offset{context.NextNumber}");
            result.Variables.Add(offset);
            result.Body.Add(Expression.Assign(offset, context.Offset()));
            return offset;
        }

        public static MethodCallExpression ParserSkipWhiteSpace<TParseContext>(this CompilationContext<TParseContext> context)
    where TParseContext : ParseContext
        {
            return Expression.Call(context.ParseContext, ExpressionHelper<TParseContext>.ParserContext_SkipWhiteSpaceMethod);
        }
}

    public static class ExpressionHelper<TParseContext>
    where TParseContext : ParseContext
    {
        internal static MethodInfo ParserContext_SkipWhiteSpaceMethod = typeof(TParseContext).GetMethod(nameof(ParseContext.SkipWhiteSpace), Array.Empty<Type>());
        internal static MethodInfo Scanner_ReadText = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadText), new[] { typeof(string), typeof(StringComparer), typeof(TokenResult) });
        internal static MethodInfo Scanner_ReadText_NoResult = typeof(Scanner).GetMethod(nameof(Parlot.Scanner.ReadText), new[] { typeof(string), typeof(StringComparer) });
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

        internal static ConstructorInfo TextSpan_Constructor = typeof(TextSpan).GetConstructor(new[] { typeof(string), typeof(int), typeof(int) });

        public static Expression NewTextSpan( CompilationContext<TParseContext> _, Expression buffer, Expression offset, Expression count) => Expression.New(TextSpan_Constructor, new[] { buffer, offset, count });
        public static MemberExpression Scanner( CompilationContext<TParseContext> context) => Expression.Field(context.ParseContext, "Scanner");
        public static MemberExpression Cursor( CompilationContext<TParseContext> context) => Expression.Field(Scanner(context), "Cursor");
        public static MemberExpression Position( CompilationContext<TParseContext> context) => Expression.Property(Cursor(context), "Position");
        public static Expression ResetPosition( CompilationContext<TParseContext> context, Expression position) => Expression.Call(Cursor(context), typeof(Cursor).GetMethod("ResetPosition"), position);
        public static MemberExpression Offset( CompilationContext<TParseContext> context) => Expression.Property(Cursor(context), "Offset");
        public static MemberExpression Offset( CompilationContext<TParseContext> context, Expression textPosition) => Expression.Field(textPosition, nameof(TextPosition.Offset));
        public static MemberExpression Current( CompilationContext<TParseContext> context) => Expression.Property(Cursor(context), "Current");
        public static MemberExpression Eof( CompilationContext<TParseContext> context) => Expression.Property(Cursor(context), "Eof");
        public static MemberExpression Buffer( CompilationContext<TParseContext> context) => Expression.Field(Scanner(context), "Buffer");
        public static Expression ThrowObject( CompilationContext<TParseContext> _, Expression o) => Expression.Throw(Expression.New(typeof(Exception).GetConstructor(new[] { typeof(string) }), Expression.Call(o, o.Type.GetMethod("ToString", new Type[0]))));
        public static Expression ThrowParseException( CompilationContext<TParseContext> context, Expression message) => Expression.Throw(Expression.New(typeof(ParseException).GetConstructors().First(), new [] { message, Position(context) } ));

        public static MethodCallExpression ReadSingleQuotedString( CompilationContext<TParseContext> context) => Expression.Call(Scanner(context), Scanner_ReadSingleQuotedString);
        public static MethodCallExpression ReadDoubleQuotedString( CompilationContext<TParseContext> context) => Expression.Call(Scanner(context), Scanner_ReadDoubleQuotedString);
        public static MethodCallExpression ReadQuotedString( CompilationContext<TParseContext> context) => Expression.Call(Scanner(context), Scanner_ReadQuotedString);
        public static MethodCallExpression ReadChar( CompilationContext<TParseContext> context, char c) => Expression.Call(Scanner(context), Scanner_ReadChar, Expression.Constant(c));
        public static MethodCallExpression ReadDecimal( CompilationContext<TParseContext> context) => Expression.Call(Scanner(context), Scanner_ReadDecimal);
        public static MethodCallExpression ReadInteger( CompilationContext<TParseContext> context) => Expression.Call(Scanner(context), Scanner_ReadInteger);
        public static MethodCallExpression ReadNonWhiteSpace( CompilationContext<TParseContext> context) => Expression.Call(Scanner(context), Scanner_ReadNonWhiteSpace);
        public static MethodCallExpression ReadNonWhiteSpaceOrNewLine( CompilationContext<TParseContext> context) => Expression.Call(Scanner(context), Scanner_ReadNonWhiteSpaceOrNewLine);
        public static MethodCallExpression SkipWhiteSpace( CompilationContext<TParseContext> context) => Expression.Call(Scanner(context), Scanner_SkipWhiteSpace);
        public static MethodCallExpression SkipWhiteSpaceOrNewLine( CompilationContext<TParseContext> context) => Expression.Call(Scanner(context), Scanner_SkipWhiteSpaceOrNewLine);
        public static MethodCallExpression Advance( CompilationContext<TParseContext> context) => Expression.Call(Cursor(context), typeof(Cursor).GetMethod(nameof(Parlot.Cursor.Advance), new Type[0] { }));

        public static ParameterExpression DeclareSuccessVariable( CompilationContext<TParseContext> context, CompilationResult result, bool defaultValue)
        {
            result.Success = Expression.Variable(typeof(bool), $"success{context.NextNumber}");
            result.Variables.Add(result.Success);
            result.Body.Add(Expression.Assign(result.Success, Expression.Constant(defaultValue, typeof(bool))));
            return result.Success;
        }

        public static ParameterExpression DeclareValueVariable<T>( CompilationContext<TParseContext> context, CompilationResult result)
        {
            return DeclareValueVariable(context, result, Expression.Default(typeof(T)));
        }
            
        public static ParameterExpression DeclareValueVariable( CompilationContext<TParseContext> context, CompilationResult result, Expression defaultValue)
        {
            result.Value = Expression.Variable(defaultValue.Type, $"value{context.NextNumber}");

            if (!context.DiscardResult)
            {
                result.Variables.Add(result.Value);
                result.Body.Add(Expression.Assign(result.Value, defaultValue));
            }

            return result.Value;
        }

        public static ParameterExpression DeclarePositionVariable( CompilationContext<TParseContext> context, CompilationResult result)
        {
            var start = Expression.Variable(typeof(TextPosition), $"position{context.NextNumber}");
            result.Variables.Add(start);
            result.Body.Add(Expression.Assign(start, Position(context)));
            return start;
        }

        public static ParameterExpression DeclareOffsetVariable( CompilationContext<TParseContext> context, CompilationResult result)
        {
            var offset = Expression.Variable(typeof(int), $"offset{context.NextNumber}");
            result.Variables.Add(offset);
            result.Body.Add(Expression.Assign(offset, Offset(context)));
            return offset;
        }

        public static MethodCallExpression ParserSkipWhiteSpace( CompilationContext<TParseContext> context)
        {
            return Expression.Call(context.ParseContext, ParserContext_SkipWhiteSpaceMethod);
        }
    }
}
