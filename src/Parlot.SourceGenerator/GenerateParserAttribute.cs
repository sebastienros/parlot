namespace Parlot.SourceGenerator;

/// <summary>
/// Marks a parser descriptor method for Parlot source generation.
/// The annotated method must be static and return Parlot.Fluent.Parser&lt;T&gt;.
/// Optionally, you can provide a factory method name that will be generated as:
///
///   public static Parlot.Fluent.Parser&lt;T&gt; {FactoryMethodName}() => new GeneratedParser_XXX();
///
/// alongside the descriptor-specific {DescriptorName}_Generated() factory.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
public sealed class GenerateParserAttribute : System.Attribute
{
    public GenerateParserAttribute()
    {
    }

    public GenerateParserAttribute(string factoryMethodName)
    {
        FactoryMethodName = factoryMethodName;
    }

    /// <summary>
    /// Optional name of a public factory method the generator should create that returns
    /// the source-generated parser.
    /// </summary>
    public string? FactoryMethodName { get; }
}
