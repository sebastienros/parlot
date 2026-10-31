using NCalc;
using Parlot.Fluent;

namespace NCalc.Benchmarks;

// NCalc's grammar is compiled in this assembly with PARLOT_FLUENT, as in NCalc's own benchmarks
internal static partial class FluentExpressionParser
{
    internal static Parser<LogicalExpression> Create(LogicalExpressionParserOptions options, CultureInfo culture)
        => CreateParserGrammar(options, culture);
}
