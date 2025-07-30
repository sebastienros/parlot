using Parlot.Fluent;

namespace Parlot.Tests.Calc;

public class FluentParserCompiledTests : CalcTests
{
    static Parser<Expression> _compiled = FluentParser.Expression.Compile();

    protected override decimal Evaluate(string text)
    {
        _compiled.TryParse(text, out var expression);
        return expression.Evaluate();
    }
}
