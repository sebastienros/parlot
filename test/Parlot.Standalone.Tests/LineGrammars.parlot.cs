using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Standalone.Tests;

public static partial class LineGrammars
{
    // Identifiers with the lines and columns of their start and end, with the line terminators of JavaScript
    [GenerateParser(nameof(Locate), NewLines = "\n\r\u2028\u2029")]
    public static Parser<string> BuildLocate() =>
        ZeroOrMany(Terms.Identifier().Then(static (context, start, end, name) =>
        {
            // The end first, then the start, as an AST builder does
            var endPosition = context.Scanner.Cursor.GetPosition(end);
            var startPosition = context.Scanner.Cursor.GetPosition(start);
            return name.ToString() + "@" + startPosition.Line + ":" + startPosition.Column + "-" + endPosition.Line + ":" + endPosition.Column;
        }))
        .Then(static list => string.Join(",", list))
        .AndSkip(Terms.Char(';'))
        .Eof();

    // The same grammar with the default line terminators
    [GenerateParser(nameof(LocateDefault))]
    public static Parser<string> BuildLocateDefault() => BuildLocate();
}
