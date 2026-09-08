using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Parlot.SourceGeneration;

namespace Parlot.SourceGenerator;

public sealed partial class ParserSourceGenerator
{
    private static readonly DiagnosticDescriptor StandaloneSignatureDescriptor = new(
        "PARLOT023", "Invalid standalone entry point",
        "Standalone factory '{0}': {1}", "Parlot.SourceGenerator",
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor StandaloneOutputDescriptor = new(
        "PARLOT024", "Parser cannot be generated standalone",
        "Standalone factory '{0}' requires unsupported runtime code: {1}", "Parlot.SourceGenerator",
        DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor StandaloneGrammarInputDescriptor = new(
        "PARLOT025", "Standalone grammar must be build-only",
        "Factory '{0}' with a named entry point must be in a .parlot.cs AdditionalFile, excluded from Compile",
        "Parlot.SourceGenerator", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor StandaloneTargetDescriptor = new(
        "PARLOT026", "Unsupported standalone target",
        "Standalone generation requires .NET Framework 4.7.2+, .NET Standard 2.0+, or .NET 8+, and C# 12 or later",
        "Parlot.SourceGenerator", DiagnosticSeverity.Error, isEnabledByDefault: true);

    private sealed class StandaloneEntryPoint
    {
        public StandaloneEntryPoint(IMethodSymbol method, MethodToGenerate factory)
        {
            Method = method;
            Factory = factory;
        }

        public IMethodSymbol Method { get; }
        public MethodToGenerate Factory { get; }
        public IParameterSymbol? CancellationTokenParameter => Method.Parameters.Length == Factory.Method.Parameters.Length + 3
            ? Method.Parameters[Method.Parameters.Length - 2]
            : null;
        public string? Source { get; set; }
    }

    private static void InitializeStandalone(IncrementalGeneratorInitializationContext context)
    {
        var grammars = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".parlot.cs", StringComparison.OrdinalIgnoreCase))
            .Select(static (file, ct) => (file.Path, Text: file.GetText(ct)))
            .Collect();

        var configuration = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
        {
            options.GlobalOptions.TryGetValue("build_property.TargetFrameworkIdentifier", out var identifier);
            options.GlobalOptions.TryGetValue("build_property.TargetFrameworkVersion", out var version);
            options.GlobalOptions.TryGetValue("build_property.MSBuildProjectDirectory", out var directory);
            options.GlobalOptions.TryGetValue("build_property.DesignTimeBuild", out var designTime);
            options.GlobalOptions.TryGetValue("build_property.BuildingProject", out var building);
            options.GlobalOptions.TryGetValue("build_property.BuildingInsideVisualStudio", out var visualStudio);
            return (Identifier: identifier, Version: version, Directory: directory,
                DesignTime: IsDesignTimeOrIdeContext(designTime, building, visualStudio));
        });

        var compiledFactories = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => IsCandidateMethod(node),
            static (syntax, _) => GetMethodToGenerate(syntax)).Where(static factory => factory is not null);
        context.RegisterSourceOutput(compiledFactories.Combine(configuration), static (output, input) =>
        {
            if (!input.Right.DesignTime && input.Left is { } factory)
            {
                output.ReportDiagnostic(Diagnostic.Create(StandaloneGrammarInputDescriptor,
                    factory.AttributeLocation, factory.Method.Name));
            }
        });

        context.RegisterSourceOutput(context.CompilationProvider.Combine(context.ParseOptionsProvider)
            .Combine(grammars).Combine(configuration), static (output, input) =>
        {
            var (((compilation, parseOptions), files), configuration) = input;
            if (files.IsDefaultOrEmpty)
            {
                return;
            }

            GenerateStandalone(output, (CSharpCompilation)compilation, (CSharpParseOptions)parseOptions,
                files, TargetFrameworkInfo.FromMsBuildProperties(configuration.Identifier ?? "", configuration.Version ?? ""),
                configuration.Directory ?? "", configuration.DesignTime);
        });
    }

    private static void GenerateStandalone(
        SourceProductionContext output,
        CSharpCompilation host,
        CSharpParseOptions options,
        ImmutableArray<(string Path, SourceText? Text)> files,
        TargetFrameworkInfo target,
        string projectDirectory,
        bool designTime)
    {
        var supportedTarget = target.Identifier switch
        {
            TargetFrameworkIdentifier.NetFramework => target.Version >= new Version(4, 7, 2),
            TargetFrameworkIdentifier.NetStandard => target.Version >= new Version(2, 0),
            TargetFrameworkIdentifier.NetCoreApp => target.Version >= new Version(8, 0),
            _ => false,
        };
        if (!designTime && (!supportedTarget || options.LanguageVersion < LanguageVersion.CSharp12))
        {
            output.ReportDiagnostic(Diagnostic.Create(StandaloneTargetDescriptor, Location.None));
            return;
        }

        var trees = new List<SyntaxTree>();
        foreach (var file in files.GroupBy(static file => file.Path, StringComparer.Ordinal).Select(static group => group.First()))
        {
            output.CancellationToken.ThrowIfCancellationRequested();
            if (file.Text is null)
            {
                output.ReportDiagnostic(Diagnostic.Create(StandaloneSignatureDescriptor, Location.None,
                    file.Path, "The grammar file could not be read."));
                return;
            }

            if (host.SyntaxTrees.Any(tree => tree.FilePath == file.Path))
            {
                output.ReportDiagnostic(Diagnostic.Create(StandaloneGrammarInputDescriptor, Location.None, file.Path));
                return;
            }

            trees.Add(CSharpSyntaxTree.ParseText(file.Text, options, file.Path, output.CancellationToken));
        }

        var compilation = host.WithOptions(host.Options.WithOutputKind(OutputKind.DynamicallyLinkedLibrary).WithMainTypeName(null));
        foreach (var tree in compilation.SyntaxTrees.ToArray())
        {
            var root = (CompilationUnitSyntax)tree.GetRoot(output.CancellationToken);
            if (root.Members.Any(static member => member is GlobalStatementSyntax))
            {
                // Grammar construction needs application types, not executable top-level startup code.
                var declarations = root.RemoveNodes(root.Members.OfType<GlobalStatementSyntax>(),
                    SyntaxRemoveOptions.KeepDirectives | SyntaxRemoveOptions.KeepEndOfLine)!;
                compilation = compilation.ReplaceSyntaxTree(tree,
                    CSharpSyntaxTree.Create(declarations, (CSharpParseOptions)tree.Options, tree.FilePath, Encoding.UTF8));
            }
        }
        compilation = compilation.AddSyntaxTrees(trees);
        if (compilation.GetTypeByMetadataName("Parlot.Fluent.Parser`1") is null)
        {
            // Compiler hosts can load analyzers from memory, leaving Assembly.Location empty.
            using var stream = typeof(ParserSourceGenerator).Assembly.GetManifestResourceStream("Parlot.GrammarAssembly")
                ?? throw new InvalidOperationException("The analyzer is missing its grammar compilation assembly.");
            using var image = new MemoryStream();
            stream.CopyTo(image);
            compilation = compilation.AddReferences(MetadataReference.CreateFromImage(image.ToArray()));
        }

        var entries = new List<StandaloneEntryPoint>();
        var factoryCount = 0;
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree);
            foreach (var declaration in tree.GetRoot(output.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var factory = GetMethodToGenerate(declaration, model);
                if (factory is null)
                {
                    continue;
                }
                factoryCount++;

                if (factory.Value.ValidationErrors is { } errors && factory.Value.ValidationArgs is { } arguments)
                {
                    if (!designTime)
                    {
                        for (var i = 0; i < errors.Length; i++)
                        {
                            output.ReportDiagnostic(Diagnostic.Create(errors[i],
                                ExternalLocation(factory.Value.AttributeLocation), arguments[i]));
                        }
                    }
                    continue;
                }

                var entry = FindStandaloneEntryPoint(factory.Value, trees,
                    compilation.GetTypeByMetadataName("System.Threading.CancellationToken"), out var error);
                if (entry is null)
                {
                    if (!designTime)
                    {
                        output.ReportDiagnostic(Diagnostic.Create(StandaloneSignatureDescriptor,
                            ExternalLocation(factory.Value.AttributeLocation), factory.Value.Method.Name, error));
                    }
                    continue;
                }

                entries.Add(entry);
            }
        }

        if (factoryCount == 0 && !designTime)
        {
            output.ReportDiagnostic(Diagnostic.Create(StandaloneSignatureDescriptor, Location.None,
                files[0].Path, "No [GenerateParser] factories were found in the grammar files."));
            return;
        }

        var generated = new List<(string HintName, SyntaxTree Tree)>();
        var entryPointKeys = new HashSet<string>(entries.Select(static entry => GetMethodKey(entry.Method)), StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entries.Count(other => SymbolEqualityComparer.Default.Equals(other.Method, entry.Method)) > 1)
            {
                if (!designTime)
                {
                    output.ReportDiagnostic(Diagnostic.Create(StandaloneSignatureDescriptor,
                        ExternalLocation(entry.Factory.AttributeLocation), entry.Factory.Method.Name,
                        "More than one factory targets the same entry point."));
                }
                continue;
            }

            if (designTime)
            {
                entry.Source = GenerateStandaloneDesignTimeStub(entry.Method);
            }
            else
            {
                try
                {
                    GenerateForMethod(output, compilation, target, entry.Factory,
                        projectDirectory, isDesignTimeBuild: false, entry, entryPointKeys);
                }
                catch (Exception exception) when (exception is NotSupportedException or InvalidOperationException or ArgumentException)
                {
                    output.ReportDiagnostic(Diagnostic.Create(StandaloneOutputDescriptor,
                        ExternalLocation(entry.Factory.AttributeLocation), entry.Factory.Method.Name, exception.Message));
                }
            }

            if (entry.Source is not null)
            {
                var hintName = $"StandaloneParser{generated.Count}.g.cs";
                generated.Add((hintName, CSharpSyntaxTree.ParseText(entry.Source, options, hintName, Encoding.UTF8)));
            }
        }

        if (generated.Count == 0)
        {
            return;
        }

        if (!designTime)
        {
            var runtime = StandaloneRuntimeSources.GetSources(options);
            generated.AddRange(runtime);
            var candidate = host.AddSyntaxTrees(generated.Select(static source => source.Tree));
            var generatedTrees = new HashSet<SyntaxTree>(generated.Select(static source => source.Tree));
            var errors = candidate.GetDiagnostics(output.CancellationToken)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                    && diagnostic.Location.SourceTree is { } tree && generatedTrees.Contains(tree))
                .ToArray();

            if (errors.Length > 0)
            {
                foreach (var error in errors.Take(5))
                {
                    output.ReportDiagnostic(Diagnostic.Create(StandaloneOutputDescriptor,
                        ExternalLocation(entries[0].Factory.AttributeLocation), entries[0].Factory.Method.Name,
                        error.GetMessage(System.Globalization.CultureInfo.InvariantCulture)));
                }
                return;
            }

            // A custom emitter must not escape the source-only runtime through a fully qualified reference.
            foreach (var source in generated)
            {
                var model = candidate.GetSemanticModel(source.Tree);
                foreach (var name in source.Tree.GetRoot(output.CancellationToken).DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    var symbol = model.GetSymbolInfo(name, output.CancellationToken).Symbol;
                    if (symbol?.ContainingAssembly?.Identity.Name is "Parlot" or "Parlot.SourceGenerator")
                    {
                        output.ReportDiagnostic(Diagnostic.Create(StandaloneOutputDescriptor,
                            ExternalLocation(entries[0].Factory.AttributeLocation), entries[0].Factory.Method.Name,
                            symbol.ToDisplayString()));
                        return;
                    }
                }
            }
        }

        foreach (var source in generated)
        {
            output.AddSource(source.HintName, source.Tree.GetText(output.CancellationToken));
        }
    }

    private static string? GetStandaloneEntryPointName(IMethodSymbol factory)
    {
        var attribute = factory.GetAttributes().FirstOrDefault(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == "Parlot.SourceGenerator.GenerateParserAttribute");
        return attribute is { ConstructorArguments.Length: 1 }
            ? attribute.ConstructorArguments[0].Value as string ?? ""
            : null;
    }

    private static StandaloneEntryPoint? FindStandaloneEntryPoint(
        MethodToGenerate factory, List<SyntaxTree> grammarTrees, INamedTypeSymbol? cancellationTokenType, out string error)
    {
        error = "Use [GenerateParser(nameof(TryParse))] with a matching static partial bool method "
            + "taking string input, the factory's configuration arguments, an optional extra CancellationToken, and an out result.";
        var name = GetStandaloneEntryPointName(factory.Method);
        var type = factory.Method.ContainingType;
        if (string.IsNullOrWhiteSpace(name) || type.ContainingType is not null || type.IsGenericType
            || type.TypeKind != TypeKind.Class || type.IsRecord)
        {
            return null;
        }

        var resultType = ((INamedTypeSymbol)factory.Method.ReturnType).TypeArguments[0];
        if (UsesParlotType(resultType) || factory.Method.Parameters.Any(static parameter => UsesParlotType(parameter.Type)))
        {
            error = "Entry point results and configuration arguments must use BCL or application-owned types, not Parlot types.";
            return null;
        }

        var candidates = type.GetMembers(name!).OfType<IMethodSymbol>().Where(method =>
            method.IsStatic && method.IsPartialDefinition && method.PartialImplementationPart is null
            && !method.IsGenericMethod && !method.ReturnsByRef && method.ReturnType.SpecialType == SpecialType.System_Boolean
            && (method.Parameters.Length == factory.Method.Parameters.Length + 2
                || method.Parameters.Length == factory.Method.Parameters.Length + 3
                    && method.Parameters[method.Parameters.Length - 2].RefKind == RefKind.None
                    && SymbolEqualityComparer.Default.Equals(method.Parameters[method.Parameters.Length - 2].Type, cancellationTokenType))
            && method.Parameters[0].Type.SpecialType == SpecialType.System_String && method.Parameters[0].RefKind == RefKind.None
            && method.Parameters[method.Parameters.Length - 1].RefKind == RefKind.Out
            && SymbolEqualityComparer.Default.Equals(method.Parameters[method.Parameters.Length - 1].Type, resultType)
            && method.DeclaringSyntaxReferences.All(reference => !grammarTrees.Contains(reference.SyntaxTree))
            && factory.Method.Parameters.Select((parameter, index) =>
                method.Parameters[index + 1].RefKind == RefKind.None
                && SymbolEqualityComparer.Default.Equals(parameter.Type, method.Parameters[index + 1].Type)).All(static matches => matches))
            .ToArray();

        return candidates.Length == 1 ? new StandaloneEntryPoint(candidates[0], factory) : null;
    }

    private static bool UsesParlotType(ITypeSymbol type)
        => type.ContainingAssembly?.Identity.Name is "Parlot" or "Parlot.SourceGenerator"
            || type is IArrayTypeSymbol array && UsesParlotType(array.ElementType)
            || type is INamedTypeSymbol named && named.TypeArguments.Any(UsesParlotType);

    private static void AppendStandaloneEntryPoint(StringBuilder source, StandaloneEntryPoint standalone, string wrapper, string core)
    {
        var entry = standalone.Method;
        var factory = standalone.Factory.Method;
        var input = EscapeIdentifier(entry.Parameters[0].Name);
        var value = EscapeIdentifier(entry.Parameters[entry.Parameters.Length - 1].Name);
        var valueType = GetParameterTypeName(entry.Parameters[entry.Parameters.Length - 1]);
        var arguments = string.Join(", ", entry.Parameters.Skip(1).Take(factory.Parameters.Length)
            .Select(static parameter => EscapeIdentifier(parameter.Name)));
        // Keep the common names "context" and "result" available to entry point parameters.
        var prefix = GetGeneratedIdentifier(factory);
        while (entry.Parameters.Any(parameter => parameter.Name.StartsWith(prefix + "_", StringComparison.Ordinal)))
        {
            prefix += "_";
        }
        var context = prefix + "_context";
        var result = prefix + "_result";
        source.AppendLine($"        {StandaloneSignature(entry)}");
        source.AppendLine("        {");
        var cancellationArgument = standalone.CancellationTokenParameter is { } token
            ? $", {EscapeIdentifier(token.Name)}"
            : "";
        var contextCreation = factory.Parameters.Length > 0
            ? $"new {wrapper}(new global::Parlot.Scanner({input}){cancellationArgument}, {arguments})"
            : $"new global::Parlot.Fluent.ParseContext(new global::Parlot.Scanner({input}){cancellationArgument})";
        source.AppendLine($"            var {context} = {contextCreation};");
        source.AppendLine($"            var {result} = new global::Parlot.ParseResult<{valueType}>();");
        source.AppendLine("            try");
        source.AppendLine("            {");
        var invocation = factory.Parameters.Length > 0 ? $"{context}.Parse" : $"{wrapper}.{core}";
        source.AppendLine($"                if ({invocation}({context}, ref {result}))");
        source.AppendLine("                {");
        source.AppendLine($"                    {value} = {result}.Value;");
        source.AppendLine("                    return true;");
        source.AppendLine("                }");
        source.AppendLine("            }");
        source.AppendLine("            catch (global::Parlot.ParseException)");
        source.AppendLine("            {");
        source.AppendLine($"                {value} = default!;");
        source.AppendLine("                return false;");
        source.AppendLine("            }");
        source.AppendLine($"            {value} = default!;");
        source.AppendLine("            return false;");
        source.AppendLine("        }");
    }

    private static string StandaloneSignature(IMethodSymbol entry)
    {
        var declaration = (MethodDeclarationSyntax)entry.DeclaringSyntaxReferences[0].GetSyntax();
        var modifiers = string.Join(" ", declaration.Modifiers.Select(static token => token.Text));
        var parameters = string.Join(", ", entry.Parameters.Select(static parameter =>
            $"{(parameter.RefKind == RefKind.Out ? "out " : "")}{GetParameterTypeName(parameter)} {EscapeIdentifier(parameter.Name)}"));
        return $"{modifiers} bool {EscapeIdentifier(entry.Name)}({parameters})";
    }

    private static string GenerateStandaloneDesignTimeStub(IMethodSymbol entry)
    {
        var ns = entry.ContainingNamespace.IsGlobalNamespace ? "" : $"namespace {entry.ContainingNamespace.ToDisplayString()};";
        return $$"""
            // <auto-generated />
            #nullable enable
            {{ns}}
            partial class {{EscapeIdentifier(entry.ContainingType.Name)}}
            {
                {{StandaloneSignature(entry)}} =>
                    throw new global::System.NotSupportedException("Standalone parsers are generated during build.");
            }
            """;
    }

    private static Compilation StubStandaloneEntryPoints(Compilation compilation, ISet<string> entryPointKeys)
    {
        // The private build compilation must be executable before any partial entry point implementations exist.
        foreach (var tree in compilation.SyntaxTrees.ToArray())
        {
            var root = tree.GetRoot();
            var model = compilation.GetSemanticModel(tree);
            var declarations = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(static method => method.Modifiers.Any(SyntaxKind.PartialKeyword)
                    && method.Body is null && method.ExpressionBody is null
                    && !(method.ReturnType is PredefinedTypeSyntax type && type.Keyword.IsKind(SyntaxKind.VoidKeyword)))
                .Where(method => model.GetDeclaredSymbol(method) is IMethodSymbol { PartialImplementationPart: null } symbol
                    && entryPointKeys.Contains(GetMethodKey(symbol)))
                .ToArray();
            if (declarations.Length == 0)
            {
                continue;
            }

            var replaced = root.ReplaceNodes(declarations, static (_, method) => method
                .WithModifiers(SyntaxFactory.TokenList(method.Modifiers.Where(static token => !token.IsKind(SyntaxKind.PartialKeyword))))
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.ThrowExpression(
                    SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName("global::System.InvalidOperationException"))
                        .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal("A generated entry point cannot be called while constructing a grammar.")))))))))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
            compilation = compilation.ReplaceSyntaxTree(tree,
                CSharpSyntaxTree.Create((CSharpSyntaxNode)replaced, (CSharpParseOptions)tree.Options, tree.FilePath));
        }
        return compilation;
    }

    private static Location? ExternalLocation(Location? location)
    {
        if (location?.SourceTree is null)
        {
            return location;
        }
        var span = location.GetLineSpan();
        return Location.Create(span.Path, location.SourceSpan, span.Span);
    }

    private static Location? GrammarDiagnosticLocation(Location? location)
        => location?.SourceTree?.FilePath.EndsWith(".parlot.cs", StringComparison.OrdinalIgnoreCase) == true
            ? ExternalLocation(location)
            : location;
}
