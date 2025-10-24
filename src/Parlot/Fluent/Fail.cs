using Parlot.Compilation;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Doesn't parse anything and fails parsing.
/// </summary>
public sealed class Fail<T> : Parser<T>, ICompilable
{
    public Fail()
    {
        Name = "Fail";
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        return context.CreateCompilationResult<T>(false, Expression.Constant(default(T), typeof(T)));
    }
}
