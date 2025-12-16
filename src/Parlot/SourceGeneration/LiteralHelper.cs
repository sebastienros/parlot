using Microsoft.CodeAnalysis.CSharp;
using System;

namespace Parlot.SourceGeneration;

/// <summary>
/// Helper class for converting values to C# literal expressions using Roslyn's SyntaxFactory.
/// This ensures correct escaping of all special characters.
/// </summary>
public static class LiteralHelper
{
    /// <summary>
    /// Converts a value to its C# literal representation.
    /// Returns null if the value type is not supported.
    /// </summary>
    public static string? ToLiteral(object? value)
    {
        if (value is null)
        {
            return "default";
        }

        // Handle enums
        if (value.GetType().IsEnum)
        {
            var enumType = value.GetType();
            var typeName = SourceGenerationContext.GetTypeName(enumType);
            return $"{typeName}.{value}";
        }

        return value switch
        {
            bool b => b ? "true" : "false",
            char c => SyntaxFactory.Literal(c).ToString(),
            string s => SyntaxFactory.Literal(s).ToString(),
            byte b => SyntaxFactory.Literal(b).ToString(),
            sbyte sb => SyntaxFactory.Literal(sb).ToString(),
            short sh => SyntaxFactory.Literal(sh).ToString(),
            ushort ush => SyntaxFactory.Literal(ush).ToString(),
            int i => SyntaxFactory.Literal(i).ToString(),
            uint ui => SyntaxFactory.Literal(ui).ToString(),
            long l => SyntaxFactory.Literal(l).ToString(),
            ulong ul => SyntaxFactory.Literal(ul).ToString(),
            float f => SyntaxFactory.Literal(f).ToString(),
            double d => SyntaxFactory.Literal(d).ToString(),
            decimal m => SyntaxFactory.Literal(m).ToString(),
            TextSpan span when span.Length == 0 => "default",
            _ => null
        };
    }

    /// <summary>
    /// Converts a char to its C# literal representation (e.g., 'a', '\n').
    /// </summary>
    public static string CharToLiteral(char c) => SyntaxFactory.Literal(c).ToString();

    /// <summary>
    /// Converts a string to its C# literal representation (e.g., "hello", "line\nbreak").
    /// </summary>
    public static string StringToLiteral(string s) => SyntaxFactory.Literal(s).ToString();

    /// <summary>
    /// Escapes a string for use inside a C# string literal (without the surrounding quotes).
    /// Useful when building string literals via interpolation.
    /// </summary>
    public static string EscapeStringContent(string s)
    {
        // Get the full literal including quotes, then strip them
        var literal = SyntaxFactory.Literal(s).ToString();
        // Remove the surrounding quotes
        return literal.Substring(1, literal.Length - 2);
    }
}
