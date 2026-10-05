using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Parlot.SourceGenerator;

// Instrument the physical generated call graph, including early returns and unwinding exceptions.
// User callbacks and local recognizer functions keep their own return semantics.
internal sealed class DiagnosticsRewriter : CSharpSyntaxRewriter
{
    private const string Runtime = "global::Parlot.Generated.ParserDiagnostics";
    private readonly string _entry;
    private bool _inside;

    private DiagnosticsRewriter(string entry) { _entry = entry; }

    internal static string Rewrite(string source, CSharpParseOptions options, string entry)
        => new DiagnosticsRewriter(entry).Visit(CSharpSyntaxTree.ParseText(source, options).GetRoot())!.ToFullString();

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.ReturnType.ToString() != "bool" || node.Body is null ||
            node.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString() != "global::Parlot.Fluent.ParseContext" ||
            !node.Body.Statements.OfType<LocalDeclarationStatementSyntax>().Any(static statement =>
                statement.Declaration.Variables.Any(static variable => variable.Identifier.ValueText == "cursor")))
            return node;

        var namedNode = node;
        if (node.Identifier.ValueText.EndsWith("_Core", System.StringComparison.Ordinal) && node.Parent is TypeDeclarationSyntax container)
        {
            namedNode = container.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(method =>
                method.Identifier.ValueText + "_Core" == node.Identifier.ValueText) ?? node;
        }
        var name = namedNode.GetLeadingTrivia().Where(static trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            .Select(static trivia => trivia.ToString().Substring(2).Trim()).LastOrDefault()
            ?? FriendlyName(node);
        _inside = true;
        var body = (BlockSyntax)Visit(node.Body)!;
        _inside = false;
        var declarations = SyntaxFactory.ParseStatement($"var __parlotTrace = {Runtime}.Enter({SymbolDisplay.FormatLiteral(name, true)}, context.Scanner.Cursor);");
        var outcome = SyntaxFactory.ParseStatement("bool? __parlotOutcome = null;");
        var guarded = SyntaxFactory.TryStatement(body, default,
            SyntaxFactory.FinallyClause(SyntaxFactory.Block(SyntaxFactory.ParseStatement(
                $"{Runtime}.Exit(__parlotTrace, __parlotOutcome, context.Scanner.Cursor);"))));
        return node.WithBody(SyntaxFactory.Block(declarations, outcome, guarded).NormalizeWhitespace());
    }

    private string FriendlyName(MethodDeclarationSyntax method)
    {
        var name = method.Identifier.ValueText;
        if (method.Parent is TypeDeclarationSyntax type && type.Identifier.ValueText.StartsWith("GeneratedParser_", System.StringComparison.Ordinal))
        {
            var prefix = type.Identifier.ValueText.Substring("GeneratedParser_".Length) + "_";
            if (name.StartsWith(prefix, System.StringComparison.Ordinal)) name = name.Substring(prefix.Length);
        }
        return name == "Core" ? _entry : name;
    }

    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) => node;
    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => node;
    public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) => node;
    public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) => node;

    public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
        => _inside && node.Expression is not null
            ? SyntaxFactory.ParseStatement($"{{ __parlotOutcome = ({node.Expression}); return __parlotOutcome.Value; }}").WithTriviaFrom(node)
            : node;

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (_inside && node.Expression is MemberAccessExpressionSyntax member &&
            member.Expression.ToString() == "cursor" && member.Name.Identifier.ValueText == "ResetPosition")
            return SyntaxFactory.ParseExpression($"{Runtime}.Reset(cursor, {node.ArgumentList.Arguments[0]})").WithTriviaFrom(node);
        return base.VisitInvocationExpression(node);
    }
}
