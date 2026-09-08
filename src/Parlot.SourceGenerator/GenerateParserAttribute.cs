namespace Parlot.SourceGenerator;

/// <summary>
/// Marks a factory in a build-only .parlot.cs file for source generation.
/// The factory must be static, non-generic, and return Parlot.Fluent.Parser&lt;T&gt;.
/// The analyzer executes it during compilation and implements the named partial parsing method
/// without a Parlot runtime assembly dependency.
/// </summary>
/// <remarks>
/// The entry point is a static partial bool method in the same class, taking a string input,
/// the factory's by-value configuration arguments, optionally an additional
/// System.Threading.CancellationToken for cooperative cancellation, and an out T result.
/// The additional cancellation token is not a factory parameter.
/// Configuration arguments may only be used in supported parse-time callbacks.
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
    /// Marks the factory for generation of the named partial parsing method.
    /// </summary>
    /// <param name="entryPoint">The name of the partial parsing method in the same class.</param>
    public GenerateParserAttribute(string entryPoint)
    {
        EntryPoint = entryPoint;
    }

    /// <summary>
    /// Gets the name of the application-facing partial parsing method.
    /// </summary>
    public string EntryPoint { get; }
}
