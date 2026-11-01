using Parlot.Fluent;
using System;

namespace Parlot.Rewriting;

/// <summary>
/// A Parser implementing this interface can be rewritten in a more optimized way.
/// The result will replace the instance.
/// </summary>
/// <remarks>
/// This interface is not used by Parlot: no parser is ever rewritten.
/// </remarks>
[Obsolete("IRewritable<T> is not used by Parlot and will be removed in a future version.")]
public interface IRewritable<T>
{
    /// <summary>
    /// Returns the parser to substitute.
    /// </summary>
    Parser<T> Rewrite();
}
