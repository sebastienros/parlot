namespace Parlot.SourceGenerator;

/// <summary>
/// Specifies additional using directives to include in the generated source code when
/// generating parsers. Use this attribute alongside <see cref="GenerateParserAttribute"/>
/// when your generated parser needs access to types from specific namespaces.
/// </summary>
/// <remarks>
/// Example usage:
/// <code>
/// [GenerateParser]
/// [IncludeUsings("System.Collections.Generic", "MyProject.Models")]
/// public static Parser&lt;Expression&gt; CreateExpressionParser()
/// {
///     // Parser implementation that uses types from the specified namespaces
/// }
/// </code>
/// 
/// You can apply this attribute to methods or container classes:
/// <code>
/// [IncludeUsings("System.Text", "System.Linq")]
/// public static class MyParsers
/// {
///     [GenerateParser]
///     public static Parser&lt;string&gt; TextParser() =&gt; ...;
///     
///     [GenerateParser]
///     public static Parser&lt;int&gt; NumberParser() =&gt; ...;
/// }
/// </code>
/// 
/// When applied to a class, the using directives will be included in all generated
/// parsers within that class.
/// </remarks>
[System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Class, AllowMultiple = false)]
#if SOURCE_GENERATOR
internal
#else
public
#endif
sealed class IncludeUsingsAttribute : System.Attribute
{
    /// <summary>
    /// Gets the namespaces to include as using directives in the generated code.
    /// </summary>
    public string[] Usings { get; }

    /// <summary>
    /// Specifies additional using directives to include in the generated parser.
    /// </summary>
    /// <param name="usings">Namespace names to include as using directives.</param>
    public IncludeUsingsAttribute(params string[] usings)
    {
        Usings = usings ?? System.Array.Empty<string>();
    }
}
