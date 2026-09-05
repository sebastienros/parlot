namespace Parlot.SourceGenerator;

/// <summary>
/// Marks a parser descriptor method for Parlot source generation using interceptors.
/// The annotated method must be static, non-generic, and return Parlot.Fluent.Parser&lt;T&gt;.
/// By-value parameters may be captured by inline parse-time callbacks, but cannot be used
/// to construct the parser graph eagerly.
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
/// Bind runtime arguments to a generated parser instance using conditional parsers:
/// <code>
/// [GenerateParser]
/// public static Parser&lt;string&gt; FooParser(bool uppercase) =&gt;
///     If(() =&gt; uppercase, Terms.Text("FOO"), Terms.Text("foo"));
/// </code>
/// Parameterless factories return a cached parser. Parameterized factories return a new bound
/// instance that should be reused. Conditions may also accept the current ParseContext.
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
