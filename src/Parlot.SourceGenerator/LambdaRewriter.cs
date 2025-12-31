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
/// </summary>
internal sealed class LambdaRewriter : CSharpSyntaxRewriter
{
    private readonly SemanticModel _semanticModel;
    private readonly Dictionary<int, LambdaInfo> _lambdas = new();
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
