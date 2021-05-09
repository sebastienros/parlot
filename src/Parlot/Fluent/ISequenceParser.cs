using Parlot.Compilation;
using System;

namespace Parlot.Fluent
{
    public interface ISkippableSequenceParser<TParseContext, TChar>
    where TParseContext : ParseContextWithScanner<Scanner<TChar>, TChar>
    where TChar : IEquatable<TChar>, IConvertible
    {
        SkippableCompilationResult[] BuildSkippableParsers(CompilationContext<TParseContext, TChar> context);
    }
}
