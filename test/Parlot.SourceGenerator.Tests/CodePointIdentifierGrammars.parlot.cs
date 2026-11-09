#nullable enable

using System.Collections.Generic;
using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Identifiers of code points, with escape sequences and surrogate pairs. Each has a string and a TextReader entry point,
// compared with the runtime parser in CodePointIdentifierTests.
public static partial class CodePointIdentifierGrammars
{
    [GenerateParser(nameof(TryParseIdentifiers))]
    private static Parser<IReadOnlyList<string>> BuildIdentifiers() =>
        ZeroOrMany(Terms.Identifier(
            static c => c is '$' or '_' || (c < 0x10000 ? char.IsLetter((char)c) : c is >= 0x1D400 and <= 0x1D7FF),
            static c => c is '$' or '_' or 0x200C or 0x200D || (c < 0x10000 ? char.IsLetterOrDigit((char)c) : c is >= 0x1D400 and <= 0x1D7FF),
            IdentifierOptions.JavaScript).Then(static x => Character.DecodeIdentifier(x).ToString())).Eof();

    // A sequence which only reads the text is generated as span code
    [GenerateParser(nameof(TryParseSpans))]
    private static Parser<IReadOnlyList<string>> BuildSpans() =>
        ZeroOrMany(Capture(Literals.Char('#').And(Literals.Identifier(
            static c => c is '$' or '_' || (c < 0x10000 ? char.IsLetter((char)c) : c is >= 0x1D400 and <= 0x1D7FF),
            static c => c is '$' or '_' or 0x200C or 0x200D || (c < 0x10000 ? char.IsLetterOrDigit((char)c) : c is >= 0x1D400 and <= 0x1D7FF),
            IdentifierOptions.UnicodeEscapes | IdentifierOptions.SurrogatePairs)).And(Literals.Char(';'))).Then(static x => x.ToString())).Eof();
}
