using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Parlot.SourceGenerator;

public sealed partial class ParserSourceGenerator
{
    // Separates the fields of a finding in ParserDiagnosticsAttribute.Suggestions, which Parlot Explorer reads
    private const char SuggestionFieldSeparator = '\n';

    // A descriptor for each rule of Parlot.Analysis.GrammarRule, created from the rules the analysis reports
    private static readonly ConcurrentDictionary<string, DiagnosticDescriptor> _grammarRuleDescriptors = new(StringComparer.Ordinal);

    private sealed class GrammarFinding
    {
        public GrammarFinding(string id, string title, string category, string helpLink, string message)
        {
            Id = id;
            Title = title;
            Category = category;
            HelpLink = helpLink;
            Message = message;
        }

        public string Id { get; }
        public string Title { get; }
        public string Category { get; }
        public string HelpLink { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Reports what Parlot.Analysis.GrammarAnalyzer finds in a grammar (docs/grammar-rules.md) as suggestions on its
    /// factory, and keeps it for the entry point of a factory that opts into diagnostics, where Parlot Explorer shows it.
    /// </summary>
    /// <remarks>
    /// The analysis is invoked through reflection, like GenerateSource, from the Parlot assembly that built the grammar.
    /// It only suggests: a failure never keeps the grammar from being generated.
    /// </remarks>
    private static void ReportGrammarFindings(SourceProductionContext context, IMethodSymbol factory, object parser, Type valueType, StandaloneEntryPoint standalone)
    {
        List<GrammarFinding> findings;

        try
        {
            findings = AnalyzeGrammar(parser, valueType);
        }
        catch (Exception exception) when (exception is TargetInvocationException or ArgumentException or InvalidOperationException or NotSupportedException or MemberAccessException)
        {
            return;
        }

        var location = GrammarDiagnosticLocation(factory.Locations.FirstOrDefault());

        foreach (var finding in findings)
        {
            var descriptor = _grammarRuleDescriptors.GetOrAdd(finding.Id, _ => new DiagnosticDescriptor(
                finding.Id,
                finding.Title,
                "{0}",
                "Parlot." + finding.Category,
                DiagnosticSeverity.Info,
                isEnabledByDefault: true,
                helpLinkUri: finding.HelpLink));

            context.ReportDiagnostic(Diagnostic.Create(descriptor, location, finding.Message));
        }

        standalone.GrammarFindings = findings;
    }

    private static List<GrammarFinding> AnalyzeGrammar(object parser, Type valueType)
    {
        var findings = new List<GrammarFinding>();

        var parserBase = parser.GetType();

        while (parserBase is not null && !(parserBase.IsGenericType && parserBase.GetGenericTypeDefinition().FullName == "Parlot.Fluent.Parser`1"))
        {
            parserBase = parserBase.BaseType;
        }

        var analyze = parserBase?.Assembly.GetType("Parlot.Analysis.GrammarAnalyzer")?.GetMethod("Analyze", BindingFlags.Public | BindingFlags.Static)?.MakeGenericMethod(valueType);

        if (analyze is null || analyze.Invoke(null, [parser]) is not IEnumerable results)
        {
            return findings;
        }

        foreach (var result in results)
        {
            var rule = Read(result, "Rule");

            findings.Add(new GrammarFinding(
                (string)Read(rule, "Id"),
                (string)Read(rule, "Title"),
                Read(rule, "Category").ToString(),
                (string)Read(rule, "HelpLink"),
                (string)Read(result, "Message")));
        }

        return findings;

        static object Read(object instance, string property) => instance.GetType().GetProperty(property)!.GetValue(instance)!;
    }

    // The value of ParserDiagnosticsAttribute.Suggestions: a string for each finding, with its fields on separate lines
    private static string FormatSuggestions(List<GrammarFinding> findings)
    {
        var builder = new StringBuilder("new string[] { ");

        for (var i = 0; i < findings.Count; i++)
        {
            var finding = findings[i];

            if (i > 0)
            {
                builder.Append(", ");
            }

            var fields = string.Join(SuggestionFieldSeparator.ToString(), finding.Id, finding.Category, finding.Title, finding.HelpLink, finding.Message);
            builder.Append(SymbolDisplay.FormatLiteral(fields, quote: true));
        }

        return builder.Append(" }").ToString();
    }
}
