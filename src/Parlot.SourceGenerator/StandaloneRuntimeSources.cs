using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Parlot.SourceGenerator;

internal static class StandaloneRuntimeSources
{
    private const string ResourcePrefix = "Parlot.StandaloneRuntime.";

    private static readonly Lazy<string> s_licenseText = new(LoadLicenseText);
    private static readonly ConcurrentDictionary<RuntimeSourcesKey, IReadOnlyList<(string HintName, SyntaxTree Tree)>> s_runtimeCache = new();

    private readonly struct RuntimeSourcesKey : IEquatable<RuntimeSourcesKey>
    {
        private readonly bool _streaming;
        private readonly bool _diagnostics;
        private readonly LanguageVersion _languageVersion;
        private readonly DocumentationMode _documentationMode;
        private readonly SourceCodeKind _kind;
        private readonly string _preprocessorSymbols;
        private readonly string _features;
        private readonly int _hashCode;

        public RuntimeSourcesKey(CSharpParseOptions options, bool streaming, bool diagnostics)
        {
            _streaming = streaming;
            _diagnostics = diagnostics;
            _languageVersion = options.LanguageVersion;
            _documentationMode = options.DocumentationMode;
            _kind = options.Kind;
            _preprocessorSymbols = options.PreprocessorSymbolNames.Any()
                ? string.Join(";", options.PreprocessorSymbolNames.OrderBy(static s => s, StringComparer.Ordinal))
                : string.Empty;
            _features = options.Features.Count > 0
                ? string.Join(";", options.Features.OrderBy(static kv => kv.Key, StringComparer.Ordinal).Select(static kv => kv.Key + "=" + kv.Value))
                : string.Empty;

            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + (_streaming ? 1 : 0);
                hash = (hash * 31) + (_diagnostics ? 1 : 0);
                hash = (hash * 31) + (int)_languageVersion;
                hash = (hash * 31) + (int)_documentationMode;
                hash = (hash * 31) + (int)_kind;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(_preprocessorSymbols);
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(_features);
                _hashCode = hash;
            }
        }

        public bool Equals(RuntimeSourcesKey other) =>
            _streaming == other._streaming
            && _diagnostics == other._diagnostics
            && _languageVersion == other._languageVersion
            && _documentationMode == other._documentationMode
            && _kind == other._kind
            && string.Equals(_preprocessorSymbols, other._preprocessorSymbols, StringComparison.Ordinal)
            && string.Equals(_features, other._features, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is RuntimeSourcesKey other && Equals(other);

        public override int GetHashCode() => _hashCode;
    }

    private static string LoadLicenseText()
    {
        var assembly = typeof(StandaloneRuntimeSources).Assembly;
        using var licenseStream = assembly.GetManifestResourceStream(ResourcePrefix + "License")
            ?? throw new InvalidOperationException("Missing embedded runtime license.");
        using var licenseReader = new StreamReader(licenseStream, Encoding.UTF8);
        return licenseReader.ReadToEnd();
    }

    private static readonly string[] SourceNames =
    [
        "Scanner.cs",
        "Cursor.cs",
        "TextPosition.cs",
        "TextSpan.cs",
        "StringPool.cs",
        "CaseCache.cs",
        "CharSet.cs",
        "ParseResult.cs",
        "ParseException.cs",
        "ParseError.cs",
        "ParseDiagnostic.cs",
        "Option.cs",
        "Interpolation.cs",
        "Character.cs",
        "Character.Class.cs",
        "Character.Generated.cs",
        "Character.Mask.cs",
        "Character.SearchValues.cs",
        "Character.Identifier.cs",
        "WhiteSpaceSet.cs",
        "Numbers.cs",
        "HexConverter.cs",
        "ThrowHelper.cs",
        "Polyfills.Numbers.cs",
        "Polyfills.Char.cs",
        "Polyfills.ArgumentNullException.cs",
        "Fluent.ParseContext.cs",
        "Fluent.HybridList.cs",
        "Fluent.ListBuilder.cs",
        "Fluent.LongestMatch.cs",
        "Fluent.OperatorList.cs",
        "Fluent.NumberOptions.cs",
        "Fluent.StringEscapes.cs",
        "Fluent.IdentifierOptions.cs",
        "Fluent.DelegateParser.cs",
        "Fluent.Parser.cs",
    ];

    // Only embedded when an entry point reads a TextReader
    private const string StreamingSourceName = "Fluent.TextReaderRefillSource.cs";

    internal static IReadOnlyList<(string HintName, SyntaxTree Tree)> GetSources(CSharpParseOptions options, bool streaming, bool diagnostics = false)
    {
        var key = new RuntimeSourcesKey(options, streaming, diagnostics);
        return s_runtimeCache.GetOrAdd(key, _ => BuildSources(options, streaming, diagnostics));
    }

    private static (string HintName, SyntaxTree Tree)[] BuildSources(CSharpParseOptions options, bool streaming, bool diagnostics)
    {
        var assembly = typeof(StandaloneRuntimeSources).Assembly;
        var sources = new List<(string HintName, SyntaxTree Tree)>(SourceNames.Length + 2);
        var rewriter = new RuntimeRewriter(rewriteDeclarations: true, downlevel: !options.PreprocessorSymbolNames.Contains("NET8_0_OR_GREATER"));
        // PARLOT_STANDALONE compiles out the runtime-only API. PARLOT_STRING_ONLY also compiles out the compacting
        // buffer, so the cursor's buffer base is a constant, when no entry point reads a TextReader.
        var symbols = new List<string> { "PARLOT_STANDALONE" };
        if (!streaming)
        {
            symbols.Add("PARLOT_STRING_ONLY");
        }

        // C# 10 consumers can't compile the generic math helpers, which use static abstract interface members
        if (options.LanguageVersion < LanguageVersion.CSharp11)
        {
            symbols.Add("PARLOT_NO_GENERIC_MATH");
        }

        var header = "// <auto-generated/>\n/*\n" + s_licenseText.Value + "\n*/\n"
            + string.Concat(symbols.Select(static symbol => "#define " + symbol + "\n")) + "#nullable enable\n";

        // Parse the doc comments whatever the consumer's mode is, so that the rewriter can strip them
        var embeddedOptions = options
            .WithLanguageVersion(LanguageVersion.Latest)
            .WithDocumentationMode(DocumentationMode.Parse)
            .WithPreprocessorSymbols(options.PreprocessorSymbolNames.Concat(symbols));

        var names = streaming ? SourceNames.Append(StreamingSourceName) : SourceNames;
        if (diagnostics) names = names.Append("ParserDiagnostics.cs");
        foreach (var name in names)
        {
            using var stream = assembly.GetManifestResourceStream(ResourcePrefix + name)
                ?? throw new InvalidOperationException($"Missing standalone runtime resource '{ResourcePrefix + name}'.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var hintName = ResourcePrefix + name.Replace(".cs", ".g.cs");
            var tree = CSharpSyntaxTree.ParseText(reader.ReadToEnd(), embeddedOptions, hintName, Encoding.UTF8);
            var root = (CompilationUnitSyntax)rewriter.Visit(tree.GetRoot());
            var text = header + root.ToFullString();
            sources.Add((hintName, CSharpSyntaxTree.ParseText(text, options, hintName, Encoding.UTF8)));
        }

        return sources.ToArray();
    }

    internal static string RewriteGeneratedSource(string source, CSharpParseOptions options)
    {
        var tree = CSharpSyntaxTree.ParseText(source, options);
        return new RuntimeRewriter(rewriteDeclarations: false, downlevel: false).Visit(tree.GetRoot()).ToFullString();
    }

    internal static SyntaxTree RewriteGeneratedSourceToTree(string source, CSharpParseOptions options, string hintName, bool diagnostics = false, string? entryPoint = null)
    {
        var tree = CSharpSyntaxTree.ParseText(source, options, hintName, Encoding.UTF8);
        SyntaxNode root = tree.GetRoot();
        if (diagnostics && entryPoint is not null)
        {
            root = DiagnosticsRewriter.RewriteRoot(root, entryPoint);
        }

        root = new RuntimeRewriter(rewriteDeclarations: false, downlevel: false).Visit(root)!;
        return CSharpSyntaxTree.Create((CSharpSyntaxNode)root, options, hintName, Encoding.UTF8);
    }

    private sealed class RuntimeRewriter : CSharpSyntaxRewriter
    {
        private readonly bool _rewriteDeclarations;
        private readonly bool _downlevel;

        public RuntimeRewriter(bool rewriteDeclarations, bool downlevel)
        {
            _rewriteDeclarations = rewriteDeclarations;
            _downlevel = downlevel;
        }

        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            var rewritten = base.VisitToken(token);
            if (!_rewriteDeclarations || !rewritten.HasStructuredTrivia)
            {
                return rewritten;
            }

            // The shared sources document the public library, and their crefs can name API that the embedded runtime
            // compiles out. The embedded types are internal, so their docs never reach the consumer's documentation
            // file, but a consumer that generates one would still bind the crefs and report CS1574 and the like.
            var leading = rewritten.LeadingTrivia;
            var trivia = new List<SyntaxTrivia>(leading.Count);
            for (var i = 0; i < leading.Count; i++)
            {
                if (leading[i].IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                    || leading[i].IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))
                {
                    // Drop the comment's indentation too
                    if (trivia.Count > 0 && trivia[trivia.Count - 1].IsKind(SyntaxKind.WhitespaceTrivia))
                    {
                        trivia.RemoveAt(trivia.Count - 1);
                    }

                    continue;
                }

                trivia.Add(leading[i]);
            }

            return trivia.Count == leading.Count ? rewritten : rewritten.WithLeadingTrivia(trivia);
        }

        public override SyntaxNode? VisitUsingDirective(UsingDirectiveSyntax node)
        {
            var rewritten = (UsingDirectiveSyntax)base.VisitUsingDirective(node)!;
            if (node.Name is not null && IsRuntimeNamespace(node.Name, out var fluent))
            {
                return rewritten.WithName(RewriteNamespace(node.Name, fluent));
            }

            return rewritten;
        }

        public override SyntaxNode? VisitNamespaceDeclaration(NamespaceDeclarationSyntax node)
        {
            var rewritten = (NamespaceDeclarationSyntax)base.VisitNamespaceDeclaration(node)!;
            return _rewriteDeclarations && IsRuntimeNamespace(node.Name, out var fluent)
                ? rewritten.WithName(RewriteNamespace(node.Name, fluent))
                : rewritten;
        }

        public override SyntaxNode? VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
        {
            var rewritten = (FileScopedNamespaceDeclarationSyntax)base.VisitFileScopedNamespaceDeclaration(node)!;
            return _rewriteDeclarations && IsRuntimeNamespace(node.Name, out var fluent)
                ? rewritten.WithName(RewriteNamespace(node.Name, fluent))
                : rewritten;
        }

        public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
        {
            var rewritten = (QualifiedNameSyntax)base.VisitQualifiedName(node)!;
            return IsRuntimeNamespace(node.Left, out var fluent) && IsRuntimeType(node.Right, fluent)
                ? rewritten.WithLeft(RewriteNamespace(node.Left, fluent))
                : rewritten;
        }

        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var rewritten = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
            if (_downlevel && node.FirstAncestorOrSelf<ClassDeclarationSyntax>()?.Identifier.ValueText
                is not ("NumberPolyfills" or "CharPolyfills" or "ArgumentNullExceptionPolyfills"))
            {
                var helper = (node.Expression.ToString(), node.Name.Identifier.ValueText) switch
                {
                    ("byte" or "sbyte" or "short" or "ushort" or "int" or "uint" or "long" or "ulong"
                        or "float" or "double" or "decimal" or "BigInteger", "TryParse") => "NumberPolyfills",
                    ("char", "IsAsciiDigit" or "IsAsciiHexDigit") => "CharPolyfills",
                    ("ArgumentNullException", "ThrowIfNull") => "ArgumentNullExceptionPolyfills",
                    _ => null,
                };
                if (helper is not null)
                {
                    return rewritten.WithExpression(SyntaxFactory.IdentifierName(helper).WithTriviaFrom(node.Expression));
                }
            }

            if (!IsRuntimeNamespace(node.Expression, out var fluent) || !IsRuntimeType(node.Name, fluent))
            {
                return rewritten;
            }

            var name = HasGlobalAlias(node.Expression) ? "global::Parlot.Generated" : "Parlot.Generated";
            if (fluent)
            {
                name += ".Fluent";
            }

            return rewritten.WithExpression(SyntaxFactory.ParseExpression(name).WithTriviaFrom(node.Expression));
        }

        public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            var rewritten = (ClassDeclarationSyntax)base.VisitClassDeclaration(node)!;
            if (_downlevel)
            {
                // Keep one shared implementation while lowering C# 14 static extensions to C# 10.
                var members = new List<MemberDeclarationSyntax>();
                foreach (var member in rewritten.Members)
                {
                    if (member is ExtensionBlockDeclarationSyntax extension)
                    {
                        members.AddRange(extension.Members);
                    }
                    else
                    {
                        members.Add(member);
                    }
                }

                rewritten = rewritten.WithMembers(SyntaxFactory.List(members));
            }

            return IsRuntimeDeclaration(node) ? rewritten.WithModifiers(Internalize(node.Modifiers)) : rewritten;
        }

        public override SyntaxNode? VisitAttributeList(AttributeListSyntax node)
        {
            if (!_downlevel)
            {
                return base.VisitAttributeList(node);
            }

            // These annotations have no runtime behavior, and the generated helpers are internal.
            // Avoid requiring PolySharp or declaring BCL-shaped attributes in the application.
            var attributes = node.Attributes.Where(static attribute =>
                attribute.Name.ToString() is not ("NotNull" or "DoesNotReturn" or "CallerArgumentExpression")).ToArray();
            return attributes.Length == 0 ? null : node.WithAttributes(SyntaxFactory.SeparatedList(attributes));
        }

        public override SyntaxNode? VisitStructDeclaration(StructDeclarationSyntax node)
        {
            var rewritten = (StructDeclarationSyntax)base.VisitStructDeclaration(node)!;
            return IsRuntimeDeclaration(node) ? rewritten.WithModifiers(Internalize(node.Modifiers)) : rewritten;
        }

        public override SyntaxNode? VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            var rewritten = (InterfaceDeclarationSyntax)base.VisitInterfaceDeclaration(node)!;
            return IsRuntimeDeclaration(node) ? rewritten.WithModifiers(Internalize(node.Modifiers)) : rewritten;
        }

        public override SyntaxNode? VisitRecordDeclaration(RecordDeclarationSyntax node)
        {
            var rewritten = (RecordDeclarationSyntax)base.VisitRecordDeclaration(node)!;
            return IsRuntimeDeclaration(node) ? rewritten.WithModifiers(Internalize(node.Modifiers)) : rewritten;
        }

        public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            var rewritten = (EnumDeclarationSyntax)base.VisitEnumDeclaration(node)!;
            return IsRuntimeDeclaration(node) ? rewritten.WithModifiers(Internalize(node.Modifiers)) : rewritten;
        }

        public override SyntaxNode? VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            var rewritten = (DelegateDeclarationSyntax)base.VisitDelegateDeclaration(node)!;
            return IsRuntimeDeclaration(node) ? rewritten.WithModifiers(Internalize(node.Modifiers)) : rewritten;
        }

        private bool IsRuntimeDeclaration(MemberDeclarationSyntax node)
        {
            return _rewriteDeclarations && node.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax;
        }

        private static SyntaxTokenList Internalize(SyntaxTokenList modifiers)
        {
            foreach (var modifier in modifiers)
            {
                if (modifier.IsKind(SyntaxKind.PublicKeyword))
                {
                    return modifiers.Replace(modifier, SyntaxFactory.Token(SyntaxKind.InternalKeyword).WithTriviaFrom(modifier));
                }
            }

            // A top-level type without an accessibility modifier is already internal.
            return modifiers;
        }

        private static NameSyntax RewriteNamespace(NameSyntax name, bool fluent)
        {
            var replacement = HasGlobalAlias(name) ? "global::Parlot.Generated" : "Parlot.Generated";
            if (fluent)
            {
                replacement += ".Fluent";
            }

            return SyntaxFactory.ParseName(replacement).WithTriviaFrom(name);
        }

        private static bool HasGlobalAlias(ExpressionSyntax expression)
        {
            return expression switch
            {
                AliasQualifiedNameSyntax alias => alias.Alias.Identifier.ValueText == "global",
                QualifiedNameSyntax qualified => HasGlobalAlias(qualified.Left),
                MemberAccessExpressionSyntax member => HasGlobalAlias(member.Expression),
                _ => false,
            };
        }

        private static bool IsRuntimeNamespace(ExpressionSyntax expression, out bool fluent)
        {
            fluent = false;
            if (IsParlotNamespace(expression))
            {
                return true;
            }

            var isFluent = expression switch
            {
                QualifiedNameSyntax { Right: IdentifierNameSyntax right } qualified
                    => right.Identifier.ValueText == "Fluent" && IsParlotNamespace(qualified.Left),
                MemberAccessExpressionSyntax { Name: IdentifierNameSyntax name } member
                    => name.Identifier.ValueText == "Fluent" && IsParlotNamespace(member.Expression),
                _ => false,
            };

            fluent = isFluent;
            return isFluent;
        }

        private static bool IsParlotNamespace(ExpressionSyntax expression)
        {
            return expression is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == "Parlot"
                || expression is AliasQualifiedNameSyntax alias
                    && alias.Alias.Identifier.ValueText == "global"
                    && alias.Name.Identifier.ValueText == "Parlot";
        }

        private static bool IsRuntimeType(SimpleNameSyntax name, bool fluent)
        {
            var arity = name is GenericNameSyntax generic ? generic.Arity : 0;
            return (fluent, name.Identifier.ValueText, arity) switch
            {
                (false, "Scanner" or "Cursor" or "TextPosition" or "TextSpan" or "StringPool" or "CaseCache" or "CharSet" or "ParseException"
                    or "ParseError" or "ParseDiagnostic" or "ParseDiagnosticSeverity" or "Character" or "CharacterMask" or "WhiteSpaceSet" or "Numbers" or "HexConverter" or "ThrowHelper" or "InterpolationScanner", 0) => true,
                (false, "ParseResult" or "Option" or "Interpolation", 1) => true,
                (true, "ParseContext" or "NumberOptions" or "StringEscapes" or "IdentifierOptions" or "StreamRefillSource" or "TextReaderRefillSource" or "OperatorList", 0) => true,
                (true, "Parser" or "HybridList" or "ListBuilder" or "ListScratch" or "DelegateParser" or "LongestMatch" or "LongestMatches", 1) => true,
                _ => false,
            };
        }
    }
}
