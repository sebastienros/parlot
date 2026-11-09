#nullable enable

using System.Collections.Generic;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// String literals with escapes other than the default ones. Each has a string and a TextReader entry point, compared with
// the runtime parser in StringEscapesTests, which repeats the helpers below.
public static partial class StringEscapesGrammars
{
    private static Parser<IReadOnlyList<string>> Strings(StringEscapes escapes) =>
        ZeroOrMany(Terms.String(StringLiteralQuotes.SingleOrDouble, escapes).Then(static span => span.ToString())).Eof();

    private static Parser<IReadOnlyList<string>> PooledStrings(StringEscapes escapes) =>
        ZeroOrMany(Terms.String(StringLiteralQuotes.SingleOrDouble, escapes).ToPooledString()).Eof();

    private static Parser<int> Captured(StringEscapes escapes) =>
        Capture(ZeroOrMany(Terms.String(StringLiteralQuotes.SingleOrDouble, escapes))).Then(static span => span.Length).Eof();

    [GenerateParser(nameof(TryParseJavaScript))]
    private static Parser<IReadOnlyList<string>> BuildJavaScript() => Strings(StringEscapes.JavaScript);

    [GenerateParser(nameof(TryParsePooledJavaScript))]
    private static Parser<IReadOnlyList<string>> BuildPooledJavaScript() => PooledStrings(StringEscapes.JavaScript);

    [GenerateParser(nameof(TryParseCapturedJavaScript))]
    private static Parser<int> BuildCapturedJavaScript() => Captured(StringEscapes.JavaScript);

    [GenerateParser(nameof(TryParseCSharp))]
    private static Parser<IReadOnlyList<string>> BuildCSharp() => Strings(StringEscapes.CSharp);

    [GenerateParser(nameof(TryParseJson))]
    private static Parser<IReadOnlyList<string>> BuildJson() => Strings(StringEscapes.Json);

    [GenerateParser(nameof(TryParsePython))]
    private static Parser<IReadOnlyList<string>> BuildPython() => Strings(StringEscapes.Python);

    [GenerateParser(nameof(TryParseJava))]
    private static Parser<IReadOnlyList<string>> BuildJava() => Strings(StringEscapes.Java);

    [GenerateParser(nameof(TryParseSql))]
    private static Parser<IReadOnlyList<string>> BuildSql() => Strings(StringEscapes.Sql);

    [GenerateParser(nameof(TryParsePooledSql))]
    private static Parser<IReadOnlyList<string>> BuildPooledSql() => PooledStrings(StringEscapes.Sql);

    [GenerateParser(nameof(TryParseCapturedSql))]
    private static Parser<int> BuildCapturedSql() => Captured(StringEscapes.Sql);
}
