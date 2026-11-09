using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The generated copies of the grammars of ThenTryBenchmarks
public partial class ThenTryBenchmarks
{
    [GenerateParser(nameof(TryParseWhenThen))]
    private static Parser<object> BuildWhenThen() => Tokens(Terms.Identifier()
        .When(static (_, name) => !IsReserved(name.Span))
        .Then<object>(static (_, start, end, name) => new Name(start, end)));

    [GenerateParser(nameof(TryParseThenTry))]
    private static Parser<object> BuildThenTry() => Tokens(Terms.Identifier()
        .ThenTry(static (ParseContext _, int start, int end, TextSpan name, out object result) =>
        {
            if (IsReserved(name.Span))
            {
                result = null!;
                return false;
            }

            result = new Name(start, end);
            return true;
        }));

    [GenerateParser(nameof(TryParseWhenThenToken))]
    private static Parser<object> BuildWhenThenToken() => Terms.Identifier()
        .When(static (_, name) => !IsReserved(name.Span))
        .Then<object>(static (_, start, end, name) => new Name(start, end));

    [GenerateParser(nameof(TryParseThenTryToken))]
    private static Parser<object> BuildThenTryToken() => Terms.Identifier()
        .ThenTry(static (ParseContext _, int start, int end, TextSpan name, out object result) =>
        {
            if (IsReserved(name.Span))
            {
                result = null!;
                return false;
            }

            result = new Name(start, end);
            return true;
        });

    private static Parser<object> Tokens(Parser<object> identifier) =>
        ZeroOrMany(OneOf(
            identifier,
            Terms.Text("var").Then<object>(static _ => "var"),
            Terms.Text("if").Then<object>(static _ => "if"),
            Terms.Text("return").Then<object>(static _ => "return"),
            Terms.Char('=').Then<object>(static _ => "="),
            Terms.Char('(').Then<object>(static _ => "("),
            Terms.Char(')').Then<object>(static _ => ")"),
            Terms.Char(',').Then<object>(static _ => ","),
            Terms.Char(';').Then<object>(static _ => ";")))
        .Eof()
        .Then<object>(static tokens => tokens.Count);
}
