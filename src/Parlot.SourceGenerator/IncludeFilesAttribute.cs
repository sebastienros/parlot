namespace Parlot.SourceGenerator;

/// <summary>
/// Specifies additional source files to include in the compilation when generating
/// source code for the parser. Use this attribute alongside <see cref="GenerateParserAttribute"/>
/// when your parser references types defined in other files.
/// </summary>
/// <remarks>
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
/// File paths are relative to the file containing the parser method.
/// </remarks>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
#if SOURCE_GENERATOR
internal
#else
public
#endif
sealed class IncludeFilesAttribute : System.Attribute
{
    /// <summary>
    /// Gets the relative paths of the files to include in the compilation.
    /// </summary>
    public string[] Files { get; }

    /// <summary>
    /// Specifies additional source files to include when generating the parser.
    /// </summary>
    /// <param name="files">Relative paths to the files to include (relative to the file containing the parser method).</param>
    public IncludeFilesAttribute(params string[] files)
    {
        Files = files ?? System.Array.Empty<string>();
    }
}
