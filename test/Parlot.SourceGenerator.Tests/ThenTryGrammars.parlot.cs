#nullable enable

using System;
using System.Collections.Generic;
using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// The generated copies of the runtime grammars of ThenTryTests
public static partial class ThenTryGrammars
{
    [GenerateParser(nameof(TryParseIdentifier))]
    public static Parser<string> BuildIdentifier() => Terms.Identifier()
        .ThenTry(static (ParseContext context, int start, int end, TextSpan name, out string result) =>
        {
            result = $"{name}@{start}-{end}";
            return !name.Span.SequenceEqual("if".AsSpan()) && !name.Span.SequenceEqual("else".AsSpan());
        });

    [GenerateParser(nameof(TryParseChoice))]
    public static Parser<string> BuildChoice() => OneOf(
        BuildIdentifier(),
        Terms.Text("if"),
        Terms.Text("else"),
        Terms.Integer().Then(static value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
        Terms.Char('(').Then(static _ => "("));

    [GenerateParser(nameof(TryParseList))]
    public static Parser<IReadOnlyList<string>> BuildList() => ZeroOrMany(BuildChoice()).Eof();

    [GenerateParser(nameof(TryParseByte))]
    public static Parser<byte> BuildByte() => Literals.Integer().ThenTry(static value => (value is >= 0 and <= 255, (byte)value));

    [GenerateParser(nameof(TryParseDiscarded))]
    public static Parser<char> BuildDiscarded() => Terms.Char('[').AndSkip(BuildByte()).AndSkip(Terms.Char(']'));

    [GenerateParser(nameof(TryParseMethodGroup))]
    public static Parser<int> BuildMethodGroup() => Terms.Integer().ThenTry<int>(Narrow);

    [GenerateParser(nameof(TryParseReserved))]
    public static Parser<string> BuildReserved(string reserved) => Terms.Identifier()
        .ThenTry((ParseContext context, int start, int end, TextSpan name, out string result) =>
        {
            result = name.ToString();
            return result != reserved;
        });
}
