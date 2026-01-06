using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Parlot.SourceGeneration;

namespace Parlot.SourceGenerator;

/// <summary>
/// Rewrites lambda expressions and method groups in a syntax tree to wrapped versions
/// that register their pointer ID when invoked. This allows the source generator to match
/// runtime-registered lambdas back to their original source code.
/// 
/// The approach: Instead of replacing the lambda body entirely (which loses type information),
/// we wrap the lambda to first register its pointer, then execute the original body.
/// 
/// Example transformation:
///   Original: x => x.ToLower()
///   Rewritten: x => { Parlot.SourceGeneration.LambdaPointer.CurrentPointer = 0; return x.ToLower(); }
/// 
/// <para>
/// <strong>Important:</strong> Only static (closure-free) lambdas are supported. Lambdas that
/// capture variables from their enclosing scope cannot be source-generated because the generated
/// code creates static methods that don't have access to the captured state.
/// </para>
/// </summary>
internal sealed class LambdaRewriter : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly Dictionary<int, LambdaInfo> _lambdas = new();
    private readonly List<CapturedVariableInfo> _capturedVariables = new();
    private int _nextPointer;

    public LambdaRewriter(SemanticModel semanticModel)
    {
        _semanticModel = semanticModel;
    }

    /// <summary>
    /// Gets all recorded lambdas with their pointers and source code.
    /// </summary>
    public IReadOnlyDictionary<int, LambdaInfo> Lambdas => _lambdas;

    /// <summary>
    /// Gets information about any captured variables (closures) detected in lambdas.
    /// If this list is non-empty, source generation will fail with appropriate diagnostics.
    /// </summary>
    public IReadOnlyList<CapturedVariableInfo> CapturedVariables => _capturedVariables;

    /// <summary>
    /// Information about a captured variable in a lambda (closure).
    /// </summary>
    public sealed record CapturedVariableInfo(
        string VariableName,
        string LambdaSource,
        Location Location);

    /// <summary>
    /// Information about a rewritten lambda.
    /// </summary>
    public sealed record LambdaInfo(
        string OriginalSource,
        bool IsMethodGroup,
        string? InferredReturnType,
        int ParameterCount,
        IReadOnlyList<string> ParameterTypes);

    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        return RewriteLambda(node, new[] { node.Parameter }, node.ExpressionBody, node.Block);
    }

    public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        return RewriteLambda(node, node.ParameterList.Parameters.ToArray(), node.ExpressionBody, node.Block);
    }

    private ParenthesizedLambdaExpressionSyntax RewriteLambda(
        LambdaExpressionSyntax originalLambda,
        ParameterSyntax[] parameters,
        ExpressionSyntax? expressionBody,
        BlockSyntax? blockBody)
    {
        var pointer = _nextPointer++;
        var originalSource = originalLambda.ToFullString().Trim();

        // Check for captured variables (closures) - these are not supported in source generation
        DetectCapturedVariables(originalLambda, parameters, originalSource);

        // Determine return type and parameter types from semantic model
        var paramTypes = new List<string>();
        string? returnType = null;

        var typeInfo = _semanticModel.GetTypeInfo(originalLambda);
        if (typeInfo.ConvertedType is INamedTypeSymbol namedType && namedType.DelegateInvokeMethod is { } invokeMethod)
        {
            returnType = invokeMethod.ReturnType.Name.ToLowerInvariant();
            foreach (var param in invokeMethod.Parameters)
            {
                paramTypes.Add(param.Type.ToDisplayString());
            }
        }

        _lambdas[pointer] = new LambdaInfo(
            originalSource,
            IsMethodGroup: false,
            returnType,
            parameters.Length,
            paramTypes);

        // Create the pointer registration statement
        // global::Parlot.SourceGeneration.LambdaPointer.CurrentPointer = {pointer};
        var registrationStatement = CreatePointerRegistrationStatement(pointer);

        BlockSyntax newBody;
        if (blockBody != null)
        {
            // Already a block body - prepend the registration
            newBody = blockBody.WithStatements(
                blockBody.Statements.Insert(0, registrationStatement));
        }
        else if (expressionBody != null)
        {
            // Expression body - convert to block with registration + return
            newBody = SyntaxFactory.Block(
                registrationStatement,
                SyntaxFactory.ReturnStatement(expressionBody));
        }
        else
        {
            // Shouldn't happen, but handle gracefully
            newBody = SyntaxFactory.Block(registrationStatement);
        }

        // Build parameter list - keep original parameters
        var parameterList = SyntaxFactory.ParameterList(
            SyntaxFactory.SeparatedList(parameters));

        // Create the new lambda with the same modifiers (static, etc.)
        var newLambda = SyntaxFactory.ParenthesizedLambdaExpression(parameterList, newBody)
            .WithModifiers(originalLambda.Modifiers);

        return newLambda;
    }

    public override SyntaxNode? VisitArgument(ArgumentSyntax node)
    {
        // Check if the argument is a method group (identifier or member access without invocation)
        if (node.Expression is IdentifierNameSyntax or MemberAccessExpressionSyntax)
        {
            var symbolInfo = _semanticModel.GetSymbolInfo(node.Expression);
            
            // Check if it resolves to a method (method group)
            if (symbolInfo.Symbol is IMethodSymbol methodSymbol ||
                (symbolInfo.CandidateSymbols.Length > 0 && symbolInfo.CandidateSymbols[0] is IMethodSymbol))
            {
                var method = (IMethodSymbol)(symbolInfo.Symbol ?? symbolInfo.CandidateSymbols[0]);
                
                var pointer = _nextPointer++;
                var originalSource = node.Expression.ToFullString().Trim();

                var paramTypes = method.Parameters.Select(p => p.Type.ToDisplayString()).ToList();
                var returnType = method.ReturnType.Name.ToLowerInvariant();

                _lambdas[pointer] = new LambdaInfo(
                    originalSource,
                    IsMethodGroup: true,
                    returnType,
                    method.Parameters.Length,
                    paramTypes);

                // Replace method group with a lambda that sets the pointer and calls the method
                // e.g., char.IsLetter becomes (char arg0) => { LambdaPointer.CurrentPointer = N; return char.IsLetter(arg0); }
                
                var parameters = method.Parameters.Select((p, i) =>
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier($"arg{i}"))
                        .WithType(SyntaxFactory.ParseTypeName(p.Type.ToDisplayString() + " ")))
                    .ToArray();

                var parameterList = SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(parameters));

                // Build registration statement
                var registrationStatement = CreatePointerRegistrationStatement(pointer);

                // Build the method invocation
                var arguments = parameters.Select((_, i) =>
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName($"arg{i}")));
                
                var methodCall = SyntaxFactory.InvocationExpression(
                    node.Expression,
                    SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));

                var body = SyntaxFactory.Block(
                    registrationStatement,
                    SyntaxFactory.ReturnStatement(methodCall));

                var stubLambda = SyntaxFactory.ParenthesizedLambdaExpression(parameterList, body);

                return node.WithExpression(stubLambda);
            }
        }

        return base.VisitArgument(node);
    }

    /// <summary>
    /// Detects captured variables (closures) in a lambda expression.
    /// Captured variables are local variables or parameters from the enclosing scope
    /// that are referenced inside the lambda but are not lambda parameters themselves.
    /// </summary>
    private void DetectCapturedVariables(
        LambdaExpressionSyntax lambda,
        ParameterSyntax[] lambdaParameters,
        string lambdaSource)
    {
        // Get all lambda parameter names
        var parameterNames = new HashSet<string>(
            lambdaParameters.Select(p => p.Identifier.Text),
            StringComparer.Ordinal);

        // Find all identifier references in the lambda body
        var body = lambda.Body;
        if (body is null)
        {
            return;
        }

        foreach (var identifier in body.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            var name = identifier.Identifier.Text;

            // Skip if it's a lambda parameter
            if (parameterNames.Contains(name))
            {
                continue;
            }

            // Get symbol info to determine what this identifier refers to
            var symbolInfo = _semanticModel.GetSymbolInfo(identifier);
            var symbol = symbolInfo.Symbol;

            if (symbol is null)
            {
                continue;
            }

            // Check if this is a captured variable (local variable or parameter from enclosing scope)
            if (symbol is ILocalSymbol localSymbol)
            {
                // This is a local variable - check if it's from outside the lambda
                if (!IsDefinedWithinLambda(localSymbol, lambda))
                {
                    _capturedVariables.Add(new CapturedVariableInfo(
                        name,
                        lambdaSource,
                        identifier.GetLocation()));
                }
            }
            else if (symbol is IParameterSymbol paramSymbol)
            {
                // This is a parameter - check if it's from the containing method (not the lambda)
                if (paramSymbol.ContainingSymbol is IMethodSymbol containingMethod &&
                    !IsLambdaMethod(containingMethod, lambda))
                {
                    _capturedVariables.Add(new CapturedVariableInfo(
                        name,
                        lambdaSource,
                        identifier.GetLocation()));
                }
            }
        }
    }

    /// <summary>
    /// Checks if a local variable is defined within the lambda expression.
    /// </summary>
    private static bool IsDefinedWithinLambda(ILocalSymbol local, LambdaExpressionSyntax lambda)
    {
        foreach (var location in local.Locations)
        {
            if (location.SourceTree == lambda.SyntaxTree)
            {
                var span = location.SourceSpan;
                if (lambda.Span.Contains(span))
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if a method symbol represents the lambda itself.
    /// </summary>
    private static bool IsLambdaMethod(IMethodSymbol method, LambdaExpressionSyntax lambda)
    {
        // Lambda methods have MethodKind.LambdaMethod or AnonymousFunction
        if (method.MethodKind is MethodKind.LambdaMethod or MethodKind.AnonymousFunction)
        {
            // Check if the method's syntax reference matches our lambda
            foreach (var syntaxRef in method.DeclaringSyntaxReferences)
            {
                if (syntaxRef.GetSyntax() == lambda)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Creates a statement that sets LambdaPointer.CurrentPointer to the given pointer value.
    /// </summary>
    private static ExpressionStatementSyntax CreatePointerRegistrationStatement(int pointer)
    {
        // global::Parlot.SourceGeneration.LambdaPointer.CurrentPointer = {pointer};
        return SyntaxFactory.ExpressionStatement(
            SyntaxFactory.AssignmentExpression(
                SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.AliasQualifiedName(
                                SyntaxFactory.IdentifierName(SyntaxFactory.Token(SyntaxKind.GlobalKeyword)),
                                SyntaxFactory.IdentifierName("Parlot")),
                            SyntaxFactory.IdentifierName("SourceGeneration")),
                        SyntaxFactory.IdentifierName("LambdaPointer")),
                    SyntaxFactory.IdentifierName("CurrentPointer")),
                SyntaxFactory.LiteralExpression(
                    SyntaxKind.NumericLiteralExpression,
                    SyntaxFactory.Literal(pointer))));
    }
}

/// <summary>
/// Result of rewriting a syntax tree with lambda stubs.
/// </summary>
internal sealed class LambdaRewriteResult
{
    public LambdaRewriteResult(
        SyntaxTree rewrittenTree,
        IReadOnlyDictionary<int, LambdaRewriter.LambdaInfo> lambdas)
    {
        RewrittenTree = rewrittenTree;
        Lambdas = lambdas;
    }

    /// <summary>
    /// The rewritten syntax tree with stub lambdas.
    /// </summary>
    public SyntaxTree RewrittenTree { get; }

    /// <summary>
    /// Map from lambda pointer to original lambda information.
    /// </summary>
    public IReadOnlyDictionary<int, LambdaRewriter.LambdaInfo> Lambdas { get; }

    /// <summary>
    /// Gets a dictionary mapping pointers to their original source code.
    /// </summary>
    public Dictionary<int, string> GetSourceCodeMap()
    {
        return Lambdas.ToDictionary(kv => kv.Key, kv => kv.Value.OriginalSource);
    }
}
