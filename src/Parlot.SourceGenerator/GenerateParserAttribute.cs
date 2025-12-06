namespace Parlot.SourceGenerator;

/// <summary>
/// Marks a parser descriptor method for Parlot source generation.
/// The annotated method must be static and return Parlot.Fluent.Parser&lt;T&gt;.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Method, AllowMultiple = false)]
public sealed class GenerateParserAttribute : System.Attribute
{
    public GenerateParserAttribute() { }
    public GenerateParserAttribute(string factoryMethodName) => FactoryMethodName = factoryMethodName;
    public string? FactoryMethodName { get; }
}
