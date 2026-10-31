using Pidgin;

namespace Parlot.Benchmarks.PidginParsers;

public static class PidginEmailParser
{
    public static readonly Parser<char, Unit> Parser = CreateParser();

    private static Parser<char, Unit> CreateParser()
    {
        var local = Parser<char>.Token(
            static value => char.IsLetterOrDigit(value) || value is '.' or '+' or '-').SkipAtLeastOnce();
        var domain = Parser<char>.Token(
            static value => char.IsLetterOrDigit(value) || value == '-').SkipAtLeastOnce();
        var suffix = Parser<char>.Token(
            static value => char.IsLetterOrDigit(value) || value is '.' or '-').SkipAtLeastOnce();

        return local.Before(Pidgin.Parser.Char('@'))
            .Before(domain)
            .Before(Pidgin.Parser.Char('.'))
            .Before(suffix)
            .Before(Parser<char>.End);
    }

    public static string Parse(string input)
    {
        _ = Parser.ParseOrThrow(input);
        return input;
    }
}
