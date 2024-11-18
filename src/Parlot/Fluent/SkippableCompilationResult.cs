using Parlot.Compilation;

namespace Parlot.Fluent;

public class SkippableCompilationResult
{
    public SkippableCompilationResult(CompilationResult compilationResult, bool skip)
    {
        CompilationResult = compilationResult;
        Skip = skip;
    }

    public CompilationResult CompilationResult { get; set; }
    public bool Skip { get; set; }
}
