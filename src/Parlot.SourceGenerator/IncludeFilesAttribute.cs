namespace Parlot.SourceGenerator;

/// <summary>
/// Specifies additional source files to include in the compilation when generating
/// source code for the parser. Use this attribute alongside <see cref="GenerateParserAttribute"/>
/// when your parser references types defined in other files.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Root Directory:</strong> All file paths are resolved relative to the directory containing 
/// the source file where the <c>[GenerateParser]</c> method is defined, not the project root or solution root.
/// Both forward slashes (/) and backslashes (\) are accepted as path separators.
/// </para>
/// 
/// <para>
/// Glob patterns are supported:
/// <list type="bullet">
/// <item><description><c>*</c> matches any characters except path separator</description></item>
/// <item><description><c>**</c> matches any characters including path separators (recursive)</description></item>
/// <item><description><c>?</c> matches any single character except path separator</description></item>
/// </list>
/// </para>
/// 
/// Example usage:
/// <code>
/// [GenerateParser]
/// [IncludeFiles("SqlAst.cs")]
/// public static Parser&lt;StatementList&gt; CreateSqlParser()
/// {
///     // Parser implementation that uses AST types from SqlAst.cs
/// }
/// </code>
/// 
/// You can specify multiple files:
/// <code>
/// [GenerateParser]
/// [IncludeFiles("Ast.cs", "Tokens.cs", "Helpers.cs")]
/// public static Parser&lt;Expression&gt; CreateExpressionParser() =&gt; ...;
/// </code>
/// 
/// Using glob patterns to include multiple files:
/// <code>
/// [GenerateParser]
/// [IncludeFiles("*.cs", "../Shared/**/*.cs")]
/// public static Parser&lt;Expression&gt; CreateExpressionParser() =&gt; ...;
/// </code>
/// 
/// You can apply this attribute to methods or container classes:
/// <code>
/// [IncludeFiles("CommonAst.cs")]
/// public static class MyParsers
/// {
///     [GenerateParser]
///     public static Parser&lt;Expression&gt; ExpressionParser() =&gt; ...;
///     
///     [GenerateParser]
///     [IncludeFiles("StatementAst.cs")]
///     public static Parser&lt;Statement&gt; StatementParser() =&gt; ...;
/// }
/// </code>
/// 
/// When applied to a class, the files will be included for all generated parsers within that class.
/// </remarks>
[System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Class, AllowMultiple = false)]
#if SOURCE_GENERATOR
internal
#else
public
#endif
sealed class IncludeFilesAttribute : System.Attribute
{
    /// <summary>
    /// Gets the relative paths or glob patterns of the files to include in the compilation.
    /// </summary>
    public string[] Files { get; }

    /// <summary>
    /// Specifies additional source files to include when generating the parser.
    /// </summary>
    /// <param name="files">
    /// Relative paths or glob patterns for files to include (relative to the file containing the parser method).
    /// Supports wildcards: * (any characters except /), ** (recursive, any characters including /), and ? (single character).
    /// Both / and \ are accepted as path separators.
    /// </param>
    public IncludeFilesAttribute(params string[] files)
    {
        Files = files ?? System.Array.Empty<string>();
    }
}
