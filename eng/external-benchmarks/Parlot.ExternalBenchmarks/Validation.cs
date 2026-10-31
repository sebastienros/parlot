using System.Security.Cryptography;
using Fluid;
using Fluid.Parser;
using NCalc;
using NCalc.Benchmarks;
using NCalc.Extensions;
using Parlot.Fluent;

namespace Parlot.ExternalBenchmarks;

/// <summary>
/// Prints what the libraries produce, so that the outputs of two Parlot variants can be compared, and how often
/// parsers run again at an offset they already ran at (the work memoization could save).
/// </summary>
internal static class Validation
{
    private static readonly string[] InvalidTemplates =
    [
        "{% if %}",
        "Hello\n\n  {{ name | }}",
        "line 1\r\nline 2\r\n{% for item in %}{% endfor %}",
        "{% assign = 3 %}",
        "{{ 'unterminated }}",
    ];

    private static readonly string[] InvalidExpressions =
    [
        "1 +",
        "(1 + 2",
        "a ==",
        "'unterminated",
        "1 + * 2",
    ];

    public static void Run()
    {
        var lines = new List<string>();
        var parser = new FluidParser();

        foreach (var template in FluidParse.Snippets.Append(FluidParse.Product).Append(FluidParse.BlogPost).Append(FluidParse.Large))
        {
            var parsed = parser.Parse(template);
            var context = new TemplateContext();
            context.SetValue("products", new[] { new { name = "a", price = 1.5m, description = "d" } });
            lines.Add($"fluid render {Hash(template)} => {Hash(parsed.Render(context))}");
        }

        foreach (var template in InvalidTemplates)
        {
            try
            {
                parser.Parse(template);
                lines.Add($"fluid error {Escape(template)} => no error");
            }
            catch (Exception e)
            {
                lines.Add($"fluid error {Escape(template)} => {e.GetType().Name}: {Escape(e.Message)}");
            }
        }

        var options = new LogicalExpressionParserOptions();
        var fluent = FluentExpressionParser.Create(options, CultureInfo.InvariantCulture);

        foreach (var (name, text) in NCalcParse.Expressions)
        {
            lines.Add($"ncalc generated {name} => {LogicalExpressionParser.Parse(text, options, CultureInfo.InvariantCulture, CancellationToken.None).ToExpressionString()}");
            lines.Add($"ncalc fluent {name} => {fluent.Parse(text).ToExpressionString()}");
        }

        foreach (var text in InvalidExpressions)
        {
            lines.Add($"ncalc generated error {Escape(text)} => {Capture(() => LogicalExpressionParser.Parse(text, options, CultureInfo.InvariantCulture, CancellationToken.None))}");
            lines.Add($"ncalc fluent error {Escape(text)} => {Capture(() => fluent.Parse(text))}");
        }

        foreach (var line in lines)
        {
            Console.WriteLine(line);
        }

        Console.WriteLine();
        Console.WriteLine("Parsers entered again at an offset they already entered at (same parse):");
        ReportReparses("fluid", FluidParse.Snippets.Append(FluidParse.Product).Append(FluidParse.BlogPost), text => new FluidParseContext(text), parser.Grammar);
        ReportReparses("ncalc fluent", NCalcParse.Expressions.Values, text => new ParseContext(new Scanner(text)), fluent);
    }

    private static void ReportReparses<T>(string library, IEnumerable<string> texts, Func<string, ParseContext> createContext, Parser<T> grammar)
    {
        long entries = 0, repeats = 0;
        var repeatedByParser = new Dictionary<string, long>();

        foreach (var text in texts)
        {
            var seen = new HashSet<(object, int)>(ReferenceTupleComparer.Instance);
            var context = createContext(text);

            context.OnEnterParser = (p, c) =>
            {
                entries++;

                if (!seen.Add((p, c.Scanner.Cursor.Offset)))
                {
                    repeats++;
                    var key = Describe(p);
                    repeatedByParser[key] = repeatedByParser.GetValueOrDefault(key) + 1;
                }
            };

            grammar.Parse(context);
        }

        Console.WriteLine($"  {library}: {entries} parser entries, {repeats} repeated ({100.0 * repeats / Math.Max(1, entries):0.0}%)");

        foreach (var (key, count) in repeatedByParser.OrderByDescending(x => x.Value).Take(8))
        {
            Console.WriteLine($"    {count,6}  {key}");
        }
    }

    private static string Describe(object parser)
    {
        var type = parser.GetType();
        var name = type.IsGenericType ? type.Name[..type.Name.IndexOf('`')] : type.Name;
        var label = type.GetProperty("Name")?.GetValue(parser) as string;

        try
        {
            var text = parser.ToString();
            return $"{name} {label ?? ""} {(text != null && text.Length < 60 ? text : "")}".Trim();
        }
        catch
        {
            return $"{name} {label}".Trim();
        }
    }

    private static string Capture(Func<object> action)
    {
        try
        {
            action();
            return "no error";
        }
        catch (Exception e)
        {
            return $"{e.GetType().Name}: {Escape(e.Message)}";
        }
    }

    private static string Hash(string text) => Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..16];

    private static string Escape(string text) => text.Replace("\r", "\\r").Replace("\n", "\\n");

    private sealed class ReferenceTupleComparer : IEqualityComparer<(object, int)>
    {
        public static readonly ReferenceTupleComparer Instance = new();

        public bool Equals((object, int) x, (object, int) y) => ReferenceEquals(x.Item1, y.Item1) && x.Item2 == y.Item2;

        public int GetHashCode((object, int) obj) => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Item1), obj.Item2);
    }
}
