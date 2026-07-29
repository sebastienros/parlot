using System;

namespace Parlot.SourceGeneration;

public interface ISourceable
{
    /// <summary>
    /// Creates a source-generation representation of a parser.
    /// </summary>
    /// <param name="context">The current source-generation context.</param>
    /// <returns>A <see cref="SourceResult"/> describing the code to emit.</returns>
    SourceResult GenerateSource(SourceGenerationContext context);
}
