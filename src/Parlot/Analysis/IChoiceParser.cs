using System.Collections.Generic;

namespace Parlot.Analysis;

/// <summary>
/// A parser that tries its alternatives in order and returns the result of the first one that succeeds.
/// </summary>
internal interface IChoiceParser
{
    /// <summary>
    /// Gets the alternatives, in the order they are tried.
    /// </summary>
    IReadOnlyList<object> Alternatives { get; }

    /// <summary>
    /// Builds the lookup table from the hints of the alternatives, as the first parse does.
    /// </summary>
    void Resolve();
}
