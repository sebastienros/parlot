using Farkle;
using Farkle.Builder;
using System.Linq;

namespace Parlot.Benchmarks.FarkleParsers;

public static class FarkleEmailParser
{
    public static readonly CharParser<object> Parser = CreateParser();

    private static CharParser<object> CreateParser()
    {
        var word = Regex.OneOf(Enumerable.Range(char.MinValue, char.MaxValue + 1)
            .Select(static value => (char)value)
            .Where(char.IsLetterOrDigit));
        var email = Regex.Join(
            (word | Regex.OneOf('.', '+', '-')).AtLeast(1),
            Regex.Literal('@'),
            (word | Regex.Literal('-')).AtLeast(1),
            Regex.Literal('.'),
            (word | Regex.OneOf('.', '-')).AtLeast(1));

        return Terminal.Create("Email", email).CaseSensitive().AutoWhitespace(false).BuildSyntaxCheck();
    }

    public static string Parse(string input)
    {
        _ = Parser.Parse(input).Value;
        return input;
    }
}
