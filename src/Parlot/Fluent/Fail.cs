using Parlot.SourceGeneration;

namespace Parlot.Fluent;

/// <summary>
/// Doesn't parse anything and fails parsing.
/// </summary>
public sealed class Fail<T> : Parser<T>, ISourceable, ILoopAnalyzable
{
    public Fail()
    {
        Name = "Fail";
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => false;

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(T), defaultSuccess: false);

        // success = false; (already set by CreateResult)
        // value = default; (already set by CreateResult)

        return result;
    }
}
