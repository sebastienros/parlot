namespace Parlot.Rewriting
{
    using Parlot.Fluent;

    /// <summary>
    /// A Parser implementing this interface can be rewritten in a more optimized way.
    /// The result will replace the instance.
    /// </summary>
    public interface IRewritable<T>
    {
        /// <summary>
        /// Returns the parser to substitute.
        /// </summary>
        Parser<T> Rewrite();
    }
}
