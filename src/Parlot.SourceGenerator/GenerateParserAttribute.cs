namespace Parlot.SourceGenerator;

/// <summary>
/// Marks a parser descriptor method for Parlot source generation using interceptors.
/// The annotated method must be static, parameterless, and return Parlot.Fluent.Parser&lt;T&gt;.
/// 
/// When applied, the source generator will:
/// 1. Execute the method at compile time to build the parser graph
/// 2. Generate optimized source code from the parser using ISourceable
/// 3. Use C# interceptors to replace calls to this method with the source-generated version
/// </summary>
/// <remarks>
/// Example usage:
/// <code>
/// [GenerateParser]
/// public static Parser&lt;string&gt; HelloParser()
/// {
///     return Terms.Text("hello");
/// }
/// 
/// // Later usage - this call will be intercepted and replaced with the generated parser
/// var parser = HelloParser();
/// </code>
/// 
/// If you need different parser variants with different configurations, create separate methods:
/// <code>
/// [GenerateParser]
/// public static Parser&lt;string&gt; FooLowerParser() =&gt; Terms.Text("foo");
/// 
/// [GenerateParser]
/// public static Parser&lt;string&gt; FooUpperParser() =&gt; Terms.Text("FOO");
/// </code>
/// </remarks>
[System.AttributeUsage(System.AttributeTargets.Method)]
#if SOURCE_GENERATOR
internal
#else
public
#endif
sealed class GenerateParserAttribute : System.Attribute
{
    /// <summary>
    /// Marks the method for source generation. Calls to this method will be intercepted
    /// and replaced with a source-generated parser implementation.
    /// </summary>
    public GenerateParserAttribute() { }
}
