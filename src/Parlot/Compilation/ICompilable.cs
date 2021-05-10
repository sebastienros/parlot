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
}
