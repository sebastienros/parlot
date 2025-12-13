namespace Parlot.SourceGenerator;

/// <summary>
/// Marks a parser descriptor method for Parlot source generation.
/// The annotated method must be static and return Parlot.Fluent.Parser&lt;T&gt;.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = true)]
#if SOURCE_GENERATOR
internal
#else
public
#endif
sealed class GenerateParserAttribute : System.Attribute
{
    /// <summary>
    /// Generates a parser using the default generated name (<c>MethodName_Parser</c>).
    /// </summary>
    public GenerateParserAttribute() { }

    /// <summary>
    /// Generates a parser and exposes it as a static property with the given name.
    /// </summary>
    public GenerateParserAttribute(string factoryMethodName)
    {
        FactoryMethodName = factoryMethodName;
    }

    /// <summary>
    /// Generates a parser and exposes it as a static property with the given name, invoking the descriptor method with the provided arguments.
    /// </summary>
    public GenerateParserAttribute(string factoryMethodName, params object?[] arguments)
    {
        FactoryMethodName = factoryMethodName;
        Arguments = arguments ?? System.Array.Empty<object?>();
    }

    /// <summary>
    /// Optional factory (static property) name to expose the generated parser.
    /// When not specified, a default property name is used (<c>MethodName_Parser</c>).
    /// </summary>
    public string? FactoryMethodName { get; }

    /// <summary>
    /// Arguments to pass to the annotated descriptor method when generating the parser.
    /// </summary>
    public object?[] Arguments { get; } = System.Array.Empty<object?>();
}
