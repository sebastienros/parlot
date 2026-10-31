using System.Collections.Generic;

namespace Parlot.Analysis;

/// <summary>
/// What a <see cref="GrammarRule"/> is about.
/// </summary>
public enum GrammarRuleCategory
{
    /// <summary>
    /// The grammar parses what it should, but slower than it could.
    /// </summary>
    Performance,

    /// <summary>
    /// The grammar doesn't parse what it seems to.
    /// </summary>
    Correctness,
}

/// <summary>
/// A pattern <see cref="GrammarAnalyzer"/> looks for in a grammar, documented in <c>docs/grammar-rules.md</c>.
/// </summary>
public sealed class GrammarRule
{
    private const string Documentation = "https://github.com/sebastienros/parlot/blob/main/docs/grammar-rules.md#";

    private GrammarRule(string id, string title, GrammarRuleCategory category)
    {
        Id = id;
        Title = title;
        Category = category;
        HelpLink = Documentation + id.ToLowerInvariant();
    }

    /// <summary>
    /// Gets the identifier of the rule, like <c>PARLOT101</c>. The source generator reports the rule with it.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets a short description of the pattern.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets what the rule is about.
    /// </summary>
    public GrammarRuleCategory Category { get; }

    /// <summary>
    /// Gets the address of the documentation of the rule.
    /// </summary>
    public string HelpLink { get; }

    /// <summary>
    /// A choice mixes alternatives that skip white space with alternatives that don't, so it can't use a lookup table.
    /// </summary>
    public static GrammarRule MixedWhiteSpaceChoice { get; } = new("PARLOT101", "Choice mixes alternatives that skip white space and alternatives that don't", GrammarRuleCategory.Performance);

    /// <summary>
    /// An alternative of a choice can't tell which chars it can start with, so it is tried whatever the next char is.
    /// </summary>
    public static GrammarRule UnseekableAlternative { get; } = new("PARLOT102", "Choice alternative is tried whatever the next char is", GrammarRuleCategory.Performance);

    /// <summary>
    /// Alternatives of a choice start with the same parser, which is parsed again when an alternative fails after it.
    /// </summary>
    public static GrammarRule SharedLeadingParser { get; } = new("PARLOT103", "Choice alternatives start with the same parser", GrammarRuleCategory.Performance);

    /// <summary>
    /// A deferred parser looks for itself in the active parsers on each invocation, because it can invoke a parser
    /// the analysis can't read before it reads anything.
    /// </summary>
    public static GrammarRule LoopDetection { get; } = new("PARLOT104", "Deferred parser keeps its loop detection", GrammarRuleCategory.Performance);

    /// <summary>
    /// A deferred parser can invoke itself before reading anything, where it fails instead of recursing.
    /// </summary>
    public static GrammarRule LeftRecursion { get; } = new("PARLOT111", "Left recursion", GrammarRuleCategory.Correctness);

    /// <summary>
    /// An alternative of a choice never fails, so the alternatives after it are never tried.
    /// </summary>
    public static GrammarRule UnreachableAlternative { get; } = new("PARLOT112", "Choice alternatives after one that never fails", GrammarRuleCategory.Correctness);

    /// <summary>
    /// An alternative of a choice matches whenever a later one would, so the later one never matches.
    /// </summary>
    public static GrammarRule ShadowedAlternative { get; } = new("PARLOT113", "Choice alternative shadowed by an earlier one", GrammarRuleCategory.Correctness);

    /// <summary>
    /// Gets every rule <see cref="GrammarAnalyzer"/> looks for.
    /// </summary>
    public static IReadOnlyList<GrammarRule> All { get; } =
    [
        MixedWhiteSpaceChoice,
        UnseekableAlternative,
        SharedLeadingParser,
        LoopDetection,
        LeftRecursion,
        UnreachableAlternative,
        ShadowedAlternative,
    ];

    /// <inheritdoc />
    public override string ToString() => Id;
}

/// <summary>
/// A place in a grammar where <see cref="GrammarAnalyzer"/> found the pattern of a <see cref="GrammarRule"/>.
/// </summary>
public sealed class GrammarFinding
{
    internal GrammarFinding(GrammarRule rule, object parser, string message)
    {
        Rule = rule;
        Parser = parser;
        Message = message;
    }

    /// <summary>
    /// Gets the rule whose pattern was found.
    /// </summary>
    public GrammarRule Rule { get; }

    /// <summary>
    /// Gets the parser where the pattern was found, like the choice whose alternatives are concerned.
    /// </summary>
    public object Parser { get; }

    /// <summary>
    /// Gets what was found and how to change the grammar, naming the parsers with <see cref="Fluent.Parser{T}.Name"/> when they have one.
    /// </summary>
    public string Message { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Rule.Id}: {Message}";
}
