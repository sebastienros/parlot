using Parlot.Compilation;

namespace Parlot.Fluent
{
    public interface ISkippableSequenceParser<TParseContext>
    where TParseContext : ParseContext
    {
        SkippableCompilationResult[] BuildSkippableParsers(CompilationContext<TParseContext> context);
    }
}
