using Parlot.Compilation;
using System;

namespace Parlot.Fluent
{
    public interface ISkippableSequenceParser<TParseContext, T>
    where TParseContext : ParseContextWithScanner<Scanner<T>, T>
    where T : IEquatable<T>, IConvertible
    {
        SkippableCompilationResult[] BuildSkippableParsers(CompilationContext<TParseContext, T> context);
    }
}
