using System;
using System.Collections.Generic;

namespace Parlot.SourceGeneration;

/// <summary>
/// A sequence, whose parts the emitters can generate one by one.
/// </summary>
internal interface ISequenceSourceable
{
    /// <summary>
    /// Gets the parsers of the sequence, in order, the type of their value, and whether the value of the sequence uses it.
    /// </summary>
    /// <remarks>
    /// A sequence of more than two parsers is the sequence of the sequence of the first ones and of the last one.
    /// </remarks>
    IReadOnlyList<(object Parser, Type ValueType, bool Keep)> Parts { get; }

    /// <summary>
    /// Returns the expression of the value of the sequence from the variables holding the values of <see cref="Parts"/>,
    /// <see langword="null"/> for those it doesn't use.
    /// </summary>
    string CreateValue(string?[] values);
}
