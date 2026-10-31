using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Parlot.SourceGeneration;
using RoslynTextSpan = Microsoft.CodeAnalysis.Text.TextSpan;

namespace Parlot.SourceGenerator;

public sealed partial class ParserSourceGenerator
{
    // Separates the fields of a finding in ParserDiagnosticsAttribute.Suggestions, which Parlot Explorer reads
    private const char SuggestionFieldSeparator = '\n';

    // The longest rewritten choice a message shows. Longer ones are only in the properties of the diagnostic.
    private const int MaxSuggestionLength = 200;

    // A descriptor for each rule of Parlot.Analysis.GrammarRule, created from the rules the analysis reports
    private static readonly ConcurrentDictionary<string, DiagnosticDescriptor> _grammarRuleDescriptors = new(StringComparer.Ordinal);

    private sealed class GrammarFinding
    {
        public GrammarFinding(string id, string title, string category, string helpLink, string message, object parser, int? alternative, GrammarFix? fix)
        {
            Id = id;
            Title = title;
            Category = category;
            HelpLink = helpLink;
            Message = message;
            Parser = parser;
            Alternative = alternative;
            Fix = fix;
        }

        public string Id { get; }
        public string Title { get; }
        public string Category { get; }
        public string HelpLink { get; }
        public string Message { get; set; }
        public object Parser { get; }
        public int? Alternative { get; }
        public GrammarFix? Fix { get; }
    }

    // Parlot.Analysis.GrammarFix, read from the Parlot assembly that built the grammar
    private sealed class GrammarFix
    {
        public GrammarFix(string kind, int[] alternatives, string title, bool preservesBehavior)
        {
            Kind = kind;
            Alternatives = alternatives;
            Title = title;
            PreservesBehavior = preservesBehavior;
        }

        public string Kind { get; }
        public int[] Alternatives { get; }
        public string Title { get; }
        public bool PreservesBehavior { get; }
    }

    // A choice as a factory writes it: the expression that builds it, and the expression of each alternative
    private sealed class ChoiceSyntax
    {
        public ChoiceSyntax(ExpressionSyntax node, List<ExpressionSyntax> alternatives, bool isOrChain)
        {
            Node = node;
            Alternatives = alternatives;
            IsOrChain = isOrChain;
        }

        public ExpressionSyntax Node { get; }
        public List<ExpressionSyntax> Alternatives { get; }
        public bool IsOrChain { get; }
    }

    // A fix as a change of the text of a grammar file, and the choice it rewrites
    private sealed class GrammarEdit
    {
        public GrammarEdit(RoslynTextSpan span, string oldText, string newText, string choice)
        {
            Span = span;
            OldText = oldText;
            NewText = newText;
            Choice = choice;
        }

        public RoslynTextSpan Span { get; }
        public string OldText { get; }
        public string NewText { get; }
        public string Choice { get; }
    }

    /// <summary>
    /// Reports what Parlot.Analysis.GrammarAnalyzer finds in a grammar (docs/grammar-rules.md) as suggestions, on the
    /// code that builds the parsers concerned, with the edit that fixes it when there is a mechanical one. Keeps them for
    /// the entry point of a factory that opts into diagnostics, where Parlot Explorer shows them.
    /// </summary>
    /// <remarks>
    /// The analysis is invoked through reflection, like GenerateSource, from the Parlot assembly that built the grammar,
    /// and the parsers are mapped to the code with the sites the factory recorded in <see cref="ParserSites"/>. It only
    /// suggests: a failure never keeps the grammar from being generated.
    /// </remarks>
    private static void ReportGrammarFindings(PreparedFactory prepared, object parser, Type valueType)
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

        var factoryLocation = GrammarDiagnosticLocation(prepared.MethodInfo.Method.Locations.FirstOrDefault());
        var siteOf = prepared.Rewriter.Sites.ToDictionary(static pair => pair.Value, static pair => pair.Key);

        foreach (var finding in findings)
        {
            var choice = FindChoiceSyntax(prepared, siteOf, finding.Parser);
            var node = choice is not null && finding.Alternative is int alternative && alternative < choice.Alternatives.Count
                ? choice.Alternatives[alternative]
                : choice?.Node ?? FindCreationSite(prepared, finding.Parser);
            var location = node is not null ? GrammarDiagnosticLocation(node.GetLocation()) : factoryLocation;
            var edit = choice is not null && finding.Fix is not null ? CreateGrammarEdit(prepared.SemanticModel, choice, finding.Fix) : null;
            var properties = ImmutableDictionary<string, string?>.Empty;

            if (edit is not null)
            {
                var fix = finding.Fix!;

                properties = properties
                    .Add("FixTitle", fix.Title)
                    .Add("FixKind", fix.Kind)
                    .Add("FixPreservesBehavior", fix.PreservesBehavior ? "true" : "false")
                    .Add("FixStart", edit.Span.Start.ToString(CultureInfo.InvariantCulture))
                    .Add("FixLength", edit.Span.Length.ToString(CultureInfo.InvariantCulture))
                    .Add("FixOldText", edit.OldText)
                    .Add("FixNewText", edit.NewText);

                finding.Message += edit.Choice.Length <= MaxSuggestionLength && edit.Choice.IndexOfAny(['\r', '\n']) < 0
                    ? $" Suggested: {edit.Choice}"
                    : $" Suggested: {fix.Title}.";
            }

            var descriptor = _grammarRuleDescriptors.GetOrAdd(finding.Id, _ => new DiagnosticDescriptor(
                finding.Id,
                finding.Title,
                "{0}",
                "Parlot." + finding.Category,
                DiagnosticSeverity.Info,
                isEnabledByDefault: true,
                helpLinkUri: finding.HelpLink));

            prepared.Standalone.GrammarDiagnostics.Add(Diagnostic.Create(descriptor, location, properties, finding.Message));
        }

        prepared.Standalone.GrammarFindings = findings;
    }

    // The factories of a build can share a helper building a part of their grammars: its findings are reported once
    private static void ReportGrammarDiagnostics(SourceProductionContext context, IEnumerable<StandaloneEntryPoint> entries)
    {
        var reported = new HashSet<(string Id, Location Location, string Message)>();

        foreach (var diagnostic in entries.SelectMany(static entry => entry.GrammarDiagnostics))
        {
            if (reported.Add((diagnostic.Id, diagnostic.Location, diagnostic.GetMessage(CultureInfo.InvariantCulture))))
            {
                context.ReportDiagnostic(diagnostic);
            }
        }
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
            var rule = Read(result, "Rule")!;
            var fix = Read(result, "Fix");

            findings.Add(new GrammarFinding(
                (string)Read(rule, "Id")!,
                (string)Read(rule, "Title")!,
                Read(rule, "Category")!.ToString(),
                (string)Read(rule, "HelpLink")!,
                (string)Read(result, "Message")!,
                Read(result, "Parser")!,
                Read(result, "Alternative") as int?,
                fix is null ? null : new GrammarFix(
                    Read(fix, "Kind")!.ToString(),
                    ((IEnumerable)Read(fix, "Alternatives")!).Cast<int>().ToArray(),
                    (string)Read(fix, "Title")!,
                    (bool)Read(fix, "PreservesBehavior")!)));
        }

        return findings;

        static object? Read(object instance, string property) => instance.GetType().GetProperty(property)!.GetValue(instance);
    }

    /// <summary>
    /// Finds the code of a choice built with <c>Or</c> or <c>OneOf</c>, or of the operators of <c>LeftAssociative</c>, when
    /// each of its alternatives can be told apart there.
    /// </summary>
    private static ChoiceSyntax? FindChoiceSyntax(PreparedFactory prepared, Dictionary<ExpressionSyntax, int> siteOf, object choice)
    {
        if (RuntimeAlternatives(choice) is not { } runtime)
        {
            return null;
        }

        var model = prepared.SemanticModel;
        InvocationExpressionSyntax? call = null;

        // The smallest call to Or or OneOf that evaluated to the choice; Named() returns it too
        foreach (var site in ParserSites.GetSites(choice))
        {
            if (prepared.Rewriter.Sites.TryGetValue(site, out var expression) && expression is InvocationExpressionSyntax invocation &&
                ParlotMethodName(model, invocation) is "Or" or "OneOf" && (call is null || invocation.Span.Length < call.Span.Length))
            {
                call = invocation;
            }
        }

        ExpressionSyntax? node = call;
        List<ExpressionSyntax>? alternatives;
        var isOrChain = false;

        if (call is not null)
        {
            isOrChain = ParlotMethodName(model, call) == "Or";
            alternatives = isOrChain ? UnfoldOr(prepared, siteOf, call) : ElementsOf(call.ArgumentList.Arguments);
        }
        else
        {
            (node, alternatives) = FindOperators(prepared, choice);
        }

        if (node is null || alternatives is null || alternatives.Count != runtime.Count)
        {
            return null;
        }

        // Each alternative that the factory recorded is the one the choice tries at that position
        for (var i = 0; i < alternatives.Count; i++)
        {
            if (siteOf.TryGetValue(alternatives[i], out var site) && !ParserSites.GetSites(runtime[i]).Contains(site))
            {
                return null;
            }
        }

        return new ChoiceSyntax(node, alternatives, isOrChain);
    }

    // The alternatives of a choice, as the analysis lists them
    private static List<object>? RuntimeAlternatives(object choice)
    {
        var type = choice.GetType();

        if (!type.IsGenericType)
        {
            return null;
        }

        switch (type.GetGenericTypeDefinition().FullName)
        {
            case "Parlot.Fluent.OneOf`1":
                return ((IEnumerable)type.GetProperty("OriginalParsers")!.GetValue(choice)!).Cast<object>().ToList();
            case "Parlot.Fluent.OneOf`3":
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                return [type.GetField("_parserA", fields)!.GetValue(choice)!, type.GetField("_parserB", fields)!.GetValue(choice)!];
            default:
                return null;
        }
    }

    // The alternatives of a chain of Or, which flattens the choices it is invoked on: a.Or(b).Or(c) is one choice
    private static List<ExpressionSyntax>? UnfoldOr(PreparedFactory prepared, Dictionary<ExpressionSyntax, int> siteOf, InvocationExpressionSyntax call)
    {
        var alternatives = new List<ExpressionSyntax>();

        for (var current = call; ;)
        {
            var arguments = current.ArgumentList.Arguments;
            ExpressionSyntax receiver;

            // receiver.Or(alternative), or Parsers.Or(receiver, alternative)
            if (arguments.Count == 1 && current.Expression is MemberAccessExpressionSyntax access)
            {
                receiver = access.Expression;
            }
            else if (arguments.Count == 2)
            {
                receiver = arguments[0].Expression;
            }
            else
            {
                return null;
            }

            alternatives.Insert(0, arguments[arguments.Count - 1].Expression);

            if (receiver is InvocationExpressionSyntax inner && ParlotMethodName(prepared.SemanticModel, inner) == "Or" &&
                siteOf.TryGetValue(inner, out var site) && ParserSites.GetParser(site)?.GetType() is { IsGenericType: true } innerType &&
                innerType.GetGenericTypeDefinition().FullName == "Parlot.Fluent.OneOf`1")
            {
                current = inner;
                continue;
            }

            alternatives.Insert(0, receiver);
            return alternatives;
        }
    }

    // The parsers an argument list passes: each argument, or the elements of a single array or collection argument
    private static List<ExpressionSyntax>? ElementsOf(SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        if (arguments.Count == 1)
        {
            switch (arguments[0].Expression)
            {
                case CollectionExpressionSyntax collection:
                    return collection.Elements.All(static element => element is ExpressionElementSyntax)
                        ? collection.Elements.Cast<ExpressionElementSyntax>().Select(static element => element.Expression).ToList()
                        : null;
                case ArrayCreationExpressionSyntax { Initializer: { } initializer }:
                    return initializer.Expressions.ToList();
                case ImplicitArrayCreationExpressionSyntax implicitArray:
                    return implicitArray.Initializer.Expressions.ToList();
            }
        }

        return arguments.Select(static argument => argument.Expression).ToList();
    }

    // The operators of LeftAssociative: the call that built the parser holding the choice, and its operators
    private static (ExpressionSyntax? Node, List<ExpressionSyntax>? Alternatives) FindOperators(PreparedFactory prepared, object choice)
    {
        foreach (var pair in prepared.Rewriter.Sites)
        {
            if (pair.Value is not InvocationExpressionSyntax invocation ||
                ParlotMethodName(prepared.SemanticModel, invocation) is not ("LeftAssociative" or "RightAssociative") ||
                ParserSites.GetParser(pair.Key) is not { } owner ||
                !owner.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Any(field => ReferenceEquals(field.GetValue(owner), choice)))
            {
                continue;
            }

            var arguments = invocation.ArgumentList.Arguments;

            // (operator, factory) tuples, or an array of operators and a factory
            var alternatives = arguments.Count > 0 && arguments[0].Expression is TupleExpressionSyntax
                ? arguments.Select(static argument => argument.Expression).ToList()
                : arguments.Count == 2 ? ElementsOf(SyntaxFactory.SingletonSeparatedList(arguments[0])) : null;

            return (invocation, alternatives);
        }

        return (null, null);
    }

    // The smallest expression that built the parser, rather than one that only read it
    private static ExpressionSyntax? FindCreationSite(PreparedFactory prepared, object parser)
    {
        ExpressionSyntax? creation = null;

        foreach (var site in ParserSites.GetSites(parser))
        {
            if (prepared.Rewriter.Sites.TryGetValue(site, out var expression) && expression is InvocationExpressionSyntax or ObjectCreationExpressionSyntax &&
                (creation is null || expression.Span.Length < creation.Span.Length))
            {
                creation = expression;
            }
        }

        return creation;
    }

    private static string? ParlotMethodName(SemanticModel model, InvocationExpressionSyntax invocation)
        => model.GetSymbolInfo(invocation).Symbol is IMethodSymbol method && (method.ReducedFrom ?? method).ContainingAssembly?.Name == "Parlot"
            ? method.Name
            : null;

    /// <summary>
    /// Writes a fix of the analysis as a change of the text of the choice, and returns it when the choice it produces
    /// parses and has the type it had.
    /// </summary>
    private static GrammarEdit? CreateGrammarEdit(SemanticModel model, ChoiceSyntax choice, GrammarFix fix)
    {
        var alternatives = choice.Alternatives;
        var replacements = new Dictionary<int, string>();
        var region = RoslynTextSpan.FromBounds(alternatives[0].SpanStart, alternatives[alternatives.Count - 1].Span.End);
        string? newRegion = null;

        switch (fix.Kind)
        {
            case "Reorder" when fix.Alternatives.Length == alternatives.Count:
                // Each alternative takes the place of the one it is tried instead of
                for (var i = 0; i < alternatives.Count; i++)
                {
                    if (fix.Alternatives[i] != i)
                    {
                        replacements[i] = alternatives[fix.Alternatives[i]].ToString();
                    }
                }

                break;

            case "SkipWhiteSpace":
                // Literals.Text("a") becomes Terms.Text("a")
                foreach (var index in fix.Alternatives)
                {
                    if (index >= alternatives.Count || TermsInsteadOfLiterals(alternatives[index]) is not { } terms)
                    {
                        return null;
                    }

                    replacements[index] = terms;
                }

                break;

            case "FactorLeadingParser":
                if (FactorLeadingParser(choice, fix.Alternatives) is not { } factored)
                {
                    return null;
                }

                (region, newRegion) = factored;
                break;

            default:
                return null;
        }

        var text = choice.Node.SyntaxTree.GetText();
        var oldRegion = text.ToString(region);

        if (newRegion is null)
        {
            var builder = new StringBuilder(oldRegion);

            foreach (var pair in replacements.OrderByDescending(static pair => pair.Key))
            {
                var span = alternatives[pair.Key].Span;
                builder.Remove(span.Start - region.Start, span.Length).Insert(span.Start - region.Start, pair.Value);
            }

            newRegion = builder.ToString();
        }

        if (newRegion == oldRegion || region.Start < choice.Node.SpanStart || region.End > choice.Node.Span.End)
        {
            return null;
        }

        // The choice with the change, bound where it is: a change that doesn't compile, or changes its type, isn't offered
        var node = text.ToString(choice.Node.Span);
        var newChoice = node.Substring(0, region.Start - choice.Node.SpanStart) + newRegion + node.Substring(region.End - choice.Node.SpanStart);
        var parsed = SyntaxFactory.ParseExpression(newChoice);
        var type = model.GetTypeInfo(choice.Node).Type;
        var newType = parsed.ContainsDiagnostics ? null : model.GetSpeculativeTypeInfo(choice.Node.SpanStart, parsed, SpeculativeBindingOption.BindAsExpression).Type;

        if (type is null || newType is null || newType.TypeKind == TypeKind.Error || !SymbolEqualityComparer.Default.Equals(type, newType))
        {
            return null;
        }

        return new GrammarEdit(region, oldRegion, newRegion, newChoice);
    }

    // Literals.X(...) as Terms.X(...), which skips white space before X
    private static string? TermsInsteadOfLiterals(ExpressionSyntax alternative)
    {
        if (alternative is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: var literals } } ||
            literals is not (IdentifierNameSyntax { Identifier.ValueText: "Literals" } or MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Literals" }))
        {
            return null;
        }

        var name = literals is MemberAccessExpressionSyntax access ? access.Name : (SimpleNameSyntax)literals;
        var offset = name.SpanStart - alternative.SpanStart;
        var text = alternative.ToString();

        return text.Substring(0, offset) + "Terms" + text.Substring(offset + name.Span.Length);
    }

    // p.And(a), p.And(b) as p.And(a.Or(b)), for consecutive alternatives that invoke the same method on the same parser
    private static (RoslynTextSpan Region, string Text)? FactorLeadingParser(ChoiceSyntax choice, int[] indexes)
    {
        if (indexes.Length < 2)
        {
            return null;
        }

        string? method = null;
        string? receiver = null;
        var followers = new List<string>();

        foreach (var index in indexes)
        {
            if (index >= choice.Alternatives.Count ||
                choice.Alternatives[index] is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access, ArgumentList.Arguments: [{ } argument] } ||
                access.Name.Identifier.ValueText is not ("And" or "AndSkip" or "SkipAnd") ||
                (method is not null && (access.Name.Identifier.ValueText != method || access.Expression.ToString() != receiver)))
            {
                return null;
            }

            method = access.Name.Identifier.ValueText;
            receiver = access.Expression.ToString();
            followers.Add(argument.Expression.ToString());
        }

        var first = choice.Alternatives[indexes[0]];
        var last = choice.Alternatives[indexes[indexes.Length - 1]];

        // In a.Or(b).Or(c), merging a and b replaces a.Or(b), merging b and c replaces "b).Or(c"
        var end = choice.IsOrChain && indexes[0] == 0
            ? (last.Parent?.Parent?.Parent as InvocationExpressionSyntax)?.Span.End
            : last.Span.End;

        if (end is not int regionEnd)
        {
            return null;
        }

        var merged = new StringBuilder(receiver).Append('.').Append(method).Append('(').Append(followers[0]);

        for (var i = 1; i < followers.Count; i++)
        {
            merged.Append(".Or(").Append(followers[i]).Append(')');
        }

        return (RoslynTextSpan.FromBounds(first.SpanStart, regionEnd), merged.Append(')').ToString());
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
