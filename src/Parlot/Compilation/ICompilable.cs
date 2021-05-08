using System;

namespace Parlot.Compilation
{
    public interface ICompilable<TParseContext>
        where TParseContext : Fluent.ParseContext
    {
        /// <summary>
        /// Creates a compiled representation of a parser.
        /// </summary>
        /// <param name="context">The current compilation context.</param>
        CompilationResult Compile(CompilationContext<TParseContext> context);
    }
    public interface ICompilable<TParseContext, T>
        where TParseContext : Fluent.ParseContextWithScanner<Scanner<T>, T>
        where T : IEquatable<T>, IConvertible
    {
        /// <summary>
        /// Creates a compiled representation of a parser.
        /// </summary>
        /// <param name="context">The current compilation context.</param>
        CompilationResult Compile(CompilationContext<TParseContext, T> context);
    }
}
