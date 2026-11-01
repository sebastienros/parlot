using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
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

    private static readonly DiagnosticDescriptor LineDirectivesDescriptor = new(
        "PARLOT027", "Invalid ParlotLineDirectives value",
        "ParlotLineDirectives must be 'absolute', 'relative' or 'none', not '{0}'; absolute paths are used",
        "Parlot.SourceGenerator", DiagnosticSeverity.Warning, isEnabledByDefault: true);

    private sealed class StandaloneEntryPoint
    {
        public StandaloneEntryPoint(IMethodSymbol method, MethodToGenerate factory, bool isReader, bool collectsDiagnostics, FailureOutput failure)
        {
            Method = method;
            Factory = factory;
            IsReader = isReader;
            CollectsDiagnostics = collectsDiagnostics;
            Failure = failure;
        }

        public IMethodSymbol Method { get; }

        // Whether the entry point parses a TextReader with a compacting buffer instead of a string
        public bool IsReader { get; }

        // Whether the entry point returns the grammar errors and warnings, in an out parameter after the result
        public bool CollectsDiagnostics { get; }

        // What the entry point returns of the furthest failure, in an out parameter after the result
        public FailureOutput Failure { get; }

        // The overloads returning the furthest failure, which call the parser generated for this entry point
        public List<StandaloneEntryPoint> FailureOverloads { get; } = [];

        // The number of out parameters after the result
        private int TrailingOutputs => CollectsDiagnostics || Failure != FailureOutput.None ? 1 : 0;

        // The Parser Explorer traces the entry points whose result is the last parameter
        public bool Diagnostics => !CollectsDiagnostics && Failure == FailureOutput.None && Factory.Method.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.Name == "GenerateParserAttribute" &&
            attribute.NamedArguments.Any(static argument => argument.Key == "Diagnostics" && argument.Value.Value is true));
        public MethodToGenerate Factory { get; }
        public IParameterSymbol ResultParameter => Method.Parameters[Method.Parameters.Length - 1 - TrailingOutputs];
        public IParameterSymbol? DiagnosticsParameter => CollectsDiagnostics ? Method.Parameters[Method.Parameters.Length - 1] : null;
        public IParameterSymbol? FailureParameter => Failure != FailureOutput.None ? Method.Parameters[Method.Parameters.Length - 1] : null;
        public IParameterSymbol? CancellationTokenParameter
            => Method.Parameters.Length == Factory.Method.Parameters.Length + 3 + TrailingOutputs
                ? Method.Parameters[ResultParameter.Ordinal - 1]
                : null;
        public string? Source { get; set; }

        // How the generated callbacks map to the grammar file
        public LineDirectives LineDirectives { get; set; } = LineDirectives.Absolute;

        // What the grammar analysis found, which a factory that opts into diagnostics hands to Parlot Explorer
        public List<GrammarFinding> GrammarFindings { get; set; } = [];

        // The diagnostics of these findings, reported once all the factories of the build ran
        public List<Diagnostic> GrammarDiagnostics { get; } = [];
    }

    // What an entry point returns of the furthest failure
    private enum FailureOutput
    {
        None,

        // out int: the offset
        Offset,

        // out (int Offset, int Line, int Column)
        Position,
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
            options.GlobalOptions.TryGetValue("build_property.ParlotLineDirectives", out var lineDirectives);
            return (Identifier: identifier, Version: version, Directory: directory,
                DesignTime: IsDesignTimeOrIdeContext(designTime, building, visualStudio), LineDirectives: lineDirectives);
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
                configuration.Directory ?? "", configuration.DesignTime, configuration.LineDirectives);
        });
    }

    private static void GenerateStandalone(
        SourceProductionContext output,
        CSharpCompilation host,
        CSharpParseOptions options,
        ImmutableArray<(string Path, SourceText? Text)> files,
        TargetFrameworkInfo target,
        string projectDirectory,
        bool designTime,
        string? lineDirectivesValue)
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

        var lineDirectives = LineDirectives.Parse(lineDirectivesValue, projectDirectory);
        if (lineDirectives is null)
        {
            if (!designTime)
            {
                output.ReportDiagnostic(Diagnostic.Create(LineDirectivesDescriptor, Location.None, lineDirectivesValue));
            }
            lineDirectives = LineDirectives.Absolute;
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
            compilation = compilation.AddReferences(MetadataReference.CreateFromImage(
                LowerReferencedVersions(image.ToArray(), compilation)));
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

                var found = FindStandaloneEntryPoints(factory.Value, trees,
                    compilation.GetTypeByMetadataName("System.Threading.CancellationToken"),
                    compilation.GetTypeByMetadataName("System.IO.TextReader"), out var error);
                if (found is null)
                {
                    if (!designTime)
                    {
                        output.ReportDiagnostic(Diagnostic.Create(StandaloneSignatureDescriptor,
                            ExternalLocation(factory.Value.AttributeLocation), factory.Value.Method.Name, error));
                    }
                    continue;
                }

                entries.AddRange(found);
            }
        }

        if (factoryCount == 0 && !designTime)
        {
            output.ReportDiagnostic(Diagnostic.Create(StandaloneSignatureDescriptor, Location.None,
                files[0].Path, "No [GenerateParser] factories were found in the grammar files."));
            return;
        }

        var entryPointKeys = new HashSet<string>(entries
            .SelectMany(static entry => entry.FailureOverloads.Prepend(entry))
            .Select(static entry => GetMethodKey(entry.Method)), StringComparer.Ordinal);
        var pending = new List<StandaloneEntryPoint>();
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
                entry.Source = GenerateStandaloneDesignTimeStub(entry.FailureOverloads.Prepend(entry).Select(static overload => overload.Method).ToArray());
                continue;
            }

            entry.LineDirectives = lineDirectives;
            pending.Add(entry);
        }

        GenerateStandaloneEntryPoints(output, compilation, target, pending, projectDirectory, entryPointKeys);
        ReportGrammarDiagnostics(output, pending);

        var generated = new List<(string HintName, SyntaxTree Tree)>();
        foreach (var entry in entries)
        {
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
            var runtime = StandaloneRuntimeSources.GetSources(options, streaming: entries.Any(static entry => entry.IsReader), diagnostics: entries.Any(static entry => entry.Diagnostics));
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

    /// <summary>
    /// Runs the factories of all the entry points from one private build of the host compilation, rather than
    /// emitting and loading the whole project once per factory.
    /// </summary>
    /// <summary>
    /// The grammar assembly is built against the latest compatibility packages, such as System.Memory 4.6.3.
    /// A project pinned to an older compatible version, like System.Memory 4.5.5, would fail with CS1705, which
    /// can't be suppressed. The image is only compiled against, the compiler host's assemblies execute the factory,
    /// so its references are lowered to the versions the project references.
    /// </summary>
    private static byte[] LowerReferencedVersions(byte[] image, Compilation compilation)
    {
        var referenced = new Dictionary<string, Version>(StringComparer.OrdinalIgnoreCase);
        foreach (var identity in compilation.ReferencedAssemblyNames)
        {
            referenced[identity.Name] = identity.Version;
        }

        using var pe = new PEReader(ImmutableArray.Create(image));
        var metadata = pe.GetMetadataReader();
        var table = pe.PEHeaders.MetadataStartOffset + metadata.GetTableMetadataOffset(TableIndex.AssemblyRef);
        var rowSize = metadata.GetTableRowSize(TableIndex.AssemblyRef);
        foreach (var handle in metadata.AssemblyReferences)
        {
            var reference = metadata.GetAssemblyReference(handle);
            if (referenced.TryGetValue(metadata.GetString(reference.Name), out var version) && version < reference.Version)
            {
                // An AssemblyRef row starts with its major, minor, build and revision numbers, as little-endian ushorts.
                var row = table + (MetadataTokens.GetRowNumber(handle) - 1) * rowSize;
                WriteUInt16(image, row, version.Major);
                WriteUInt16(image, row + 2, version.Minor);
                WriteUInt16(image, row + 4, version.Build);
                WriteUInt16(image, row + 6, version.Revision);
            }
        }

        return image;

        static void WriteUInt16(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
        }
    }

    private static void GenerateStandaloneEntryPoints(
        SourceProductionContext output,
        Compilation compilation,
        TargetFrameworkInfo target,
        List<StandaloneEntryPoint> entries,
        string projectDirectory,
        ISet<string> entryPointKeys)
    {
        var shared = new List<PreparedFactory>();
        var rewrittenTrees = new Dictionary<SyntaxTree, (int FirstPointer, SyntaxTree Rewritten)>();
        var semanticModels = new Dictionary<SyntaxTree, SemanticModel>();
        var nextPointer = 0;
        foreach (var entry in entries)
        {
            // Included files and generators change the compilation, so these factories keep a build of their own
            if (entry.Factory.AdditionalFiles.Length > 0 || entry.Factory.AdditionalGenerators.Length > 0)
            {
                GenerateStandaloneEntryPoint(output, compilation, target, entry, projectDirectory, entryPointKeys);
                continue;
            }

            try
            {
                // Pointers are unique across files, so a lambda from another file is never mistaken for one of the factory's
                var tree = entry.Factory.Method.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree;
                var firstPointer = nextPointer;
                if (tree is not null && rewrittenTrees.TryGetValue(tree, out var known))
                {
                    firstPointer = known.FirstPointer;
                }

                var prepared = PrepareFactory(output, compilation, entry.Factory, entry, firstPointer, semanticModels);
                if (prepared is null)
                {
                    continue;
                }

                if (!rewrittenTrees.ContainsKey(prepared.OriginalTree))
                {
                    rewrittenTrees.Add(prepared.OriginalTree, (firstPointer, prepared.RewrittenTree));
                    nextPointer = prepared.Rewriter.NextPointer;
                }

                shared.Add(prepared);
            }
            catch (Exception exception) when (IsStandaloneOutputException(exception))
            {
                ReportStandaloneOutputException(output, entry, exception);
            }
        }

        if (shared.Count == 0)
        {
            return;
        }

        var tempCompilation = compilation;
        foreach (var pair in rewrittenTrees)
        {
            tempCompilation = tempCompilation.ReplaceSyntaxTree(pair.Key, pair.Value.Rewritten);
        }

        tempCompilation = StubStandaloneEntryPoints(tempCompilation, entryPointKeys);

        using var peStream = new MemoryStream();
        if (!EmitFactoryAssembly(ref tempCompilation, peStream, out _).Success)
        {
            // Rewriting one file can break the build of all of them, so each factory retries alone and reports its own errors
            foreach (var prepared in shared)
            {
                GenerateStandaloneEntryPoint(output, compilation, target, prepared.Standalone, projectDirectory, entryPointKeys);
            }
            return;
        }

        ExecuteFactories(output, compilation, target, peStream, shared);
    }

    private static void GenerateStandaloneEntryPoint(
        SourceProductionContext output,
        Compilation compilation,
        TargetFrameworkInfo target,
        StandaloneEntryPoint entry,
        string projectDirectory,
        ISet<string> entryPointKeys)
    {
        try
        {
            GenerateForMethod(output, compilation, target, entry.Factory,
                projectDirectory, isDesignTimeBuild: false, entry, entryPointKeys);
        }
        catch (Exception exception) when (IsStandaloneOutputException(exception))
        {
            ReportStandaloneOutputException(output, entry, exception);
        }
    }

    private static bool IsStandaloneOutputException(Exception exception)
        => exception is NotSupportedException or InvalidOperationException or ArgumentException;

    private static void ReportStandaloneOutputException(SourceProductionContext output, StandaloneEntryPoint entry, Exception exception)
        => output.ReportDiagnostic(Diagnostic.Create(StandaloneOutputDescriptor,
            ExternalLocation(entry.Factory.AttributeLocation), entry.Factory.Method.Name, exception.Message));

    private static string? GetStandaloneEntryPointName(IMethodSymbol factory)
    {
        var attribute = factory.GetAttributes().FirstOrDefault(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == "Parlot.SourceGenerator.GenerateParserAttribute");
        return attribute is { ConstructorArguments.Length: 1 }
            ? attribute.ConstructorArguments[0].Value as string ?? ""
            : null;
    }

    private static StandaloneEntryPoint[]? FindStandaloneEntryPoints(
        MethodToGenerate factory, List<SyntaxTree> grammarTrees, INamedTypeSymbol? cancellationTokenType,
        INamedTypeSymbol? textReaderType, out string error)
    {
        error = "Use [GenerateParser(nameof(TryParse))] with a matching static partial bool method "
            + "taking string input, the factory's configuration arguments, an optional extra CancellationToken, and an out result. "
            + "Other overloads with the same name may take a TextReader instead of the string, or add a last "
            + "out IReadOnlyList<(string Message, bool IsWarning, int Offset, int Line, int Column)> receiving the diagnostics, "
            + "or a last out int or out (int Offset, int Line, int Column) receiving the furthest failure.";
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

        var candidates = type.GetMembers(name!).OfType<IMethodSymbol>()
            .Select(method =>
            {
                var collects = HasDiagnosticsOutput(method, factory.Method, resultType);
                return (Method: method, Collects: collects,
                    Failure: collects ? FailureOutput.None : GetFailureOutput(method, factory.Method, resultType));
            })
            .Where(candidate =>
            {
                var method = candidate.Method;
                var extra = candidate.Collects || candidate.Failure != FailureOutput.None ? 1 : 0;
                var resultIndex = method.Parameters.Length - 1 - extra;
                return method.IsStatic && method.IsPartialDefinition && method.PartialImplementationPart is null
                    && !method.IsGenericMethod && !method.ReturnsByRef && method.ReturnType.SpecialType == SpecialType.System_Boolean
                    && (method.Parameters.Length == factory.Method.Parameters.Length + 2 + extra
                        || method.Parameters.Length == factory.Method.Parameters.Length + 3 + extra
                            && method.Parameters[resultIndex - 1].RefKind == RefKind.None
                            && SymbolEqualityComparer.Default.Equals(method.Parameters[resultIndex - 1].Type, cancellationTokenType))
                    && (method.Parameters[0].Type.SpecialType == SpecialType.System_String
                        || SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, textReaderType))
                    && method.Parameters[0].RefKind == RefKind.None
                    && method.Parameters[resultIndex].RefKind == RefKind.Out
                    && SymbolEqualityComparer.Default.Equals(method.Parameters[resultIndex].Type, resultType)
                    && method.DeclaringSyntaxReferences.All(reference => !grammarTrees.Contains(reference.SyntaxTree))
                    && factory.Method.Parameters.Select((parameter, index) =>
                        method.Parameters[index + 1].RefKind == RefKind.None
                        && SymbolEqualityComparer.Default.Equals(parameter.Type, method.Parameters[index + 1].Type)).All(static matches => matches);
            })
            .ToArray();

        // At most one entry point of each kind: string or TextReader input, returning the diagnostics, the furthest failure, or neither
        if (candidates.Length == 0 || candidates
            .GroupBy(static candidate => (candidate.Method.Parameters[0].Type.SpecialType == SpecialType.System_String, candidate.Collects, candidate.Failure))
            .Any(static group => group.Count() > 1))
        {
            return null;
        }

        var entries = new List<StandaloneEntryPoint>();
        foreach (var candidate in candidates.OrderBy(static candidate => candidate.Failure))
        {
            var entry = new StandaloneEntryPoint(candidate.Method, factory,
                isReader: candidate.Method.Parameters[0].Type.SpecialType != SpecialType.System_String,
                collectsDiagnostics: candidate.Collects, candidate.Failure);

            // An overload returning the furthest failure calls the parser of the overload with the same input which doesn't,
            // or of another one returning the failure, rather than generating it again
            var parser = entry.Failure == FailureOutput.None ? null : entries.FirstOrDefault(other =>
                other.IsReader == entry.IsReader && !other.CollectsDiagnostics);
            if (parser is null)
            {
                entries.Add(entry);
                continue;
            }

            if ((parser.CancellationTokenParameter is null) != (entry.CancellationTokenParameter is null))
            {
                error = $"The overload '{entry.Method.ToDisplayString()}' returns the furthest failure of the parser generated "
                    + $"for '{parser.Method.ToDisplayString()}', so both must take a CancellationToken or neither. "
                    + "Declare the other one as a normal forwarding method.";
                return null;
            }

            parser.FailureOverloads.Add(entry);
        }

        return entries.ToArray();
    }

    // Whether the entry point ends with the out result followed by an out int, or an out (int Offset, int Line, int Column),
    // receiving the furthest failure
    private static FailureOutput GetFailureOutput(IMethodSymbol method, IMethodSymbol factory, ITypeSymbol resultType)
    {
        if (method.Parameters.Length < factory.Parameters.Length + 3)
        {
            return FailureOutput.None;
        }

        var result = method.Parameters[method.Parameters.Length - 2];
        var failure = method.Parameters[method.Parameters.Length - 1];
        if (result.RefKind != RefKind.Out || !SymbolEqualityComparer.Default.Equals(result.Type, resultType)
            || failure.RefKind != RefKind.Out)
        {
            return FailureOutput.None;
        }

        if (failure.Type.SpecialType == SpecialType.System_Int32)
        {
            return FailureOutput.Offset;
        }

        return failure.Type is INamedTypeSymbol { IsTupleType: true } tuple
            && tuple.TupleElements.Length == 3
            && tuple.TupleElements.All(static element => element.Type.SpecialType == SpecialType.System_Int32)
                ? FailureOutput.Position
                : FailureOutput.None;
    }

    // Whether the entry point ends with the out result followed by an
    // out IReadOnlyList<(string Message, bool IsWarning, int Offset, int Line, int Column)> receiving the diagnostics
    private static bool HasDiagnosticsOutput(IMethodSymbol method, IMethodSymbol factory, ITypeSymbol resultType)
    {
        if (method.Parameters.Length < factory.Parameters.Length + 3)
        {
            return false;
        }

        var result = method.Parameters[method.Parameters.Length - 2];
        var diagnostics = method.Parameters[method.Parameters.Length - 1];

        return result.RefKind == RefKind.Out && SymbolEqualityComparer.Default.Equals(result.Type, resultType)
            && diagnostics.RefKind == RefKind.Out
            && diagnostics.Type is INamedTypeSymbol { Name: "IReadOnlyList", Arity: 1 } list
            && list.ContainingNamespace.ToDisplayString() == "System.Collections.Generic"
            && list.TypeArguments[0] is INamedTypeSymbol { IsTupleType: true } tuple
            && tuple.TupleElements.Length == 5
            && tuple.TupleElements[0].Type.SpecialType == SpecialType.System_String
            && tuple.TupleElements[1].Type.SpecialType == SpecialType.System_Boolean
            && tuple.TupleElements.Skip(2).All(static element => element.Type.SpecialType == SpecialType.System_Int32);
    }

    private static bool UsesParlotType(ITypeSymbol type)
        => type.ContainingAssembly?.Identity.Name is "Parlot" or "Parlot.SourceGenerator"
            || type is IArrayTypeSymbol array && UsesParlotType(array.ElementType)
            || type is INamedTypeSymbol named && named.TypeArguments.Any(UsesParlotType);

    private static void AppendStandaloneEntryPoint(StringBuilder source, StandaloneEntryPoint standalone, string wrapper, string core, bool wrapperIsContext)
    {
        var entry = standalone.Method;
        var factory = standalone.Factory.Method;
        var input = EscapeIdentifier(entry.Parameters[0].Name);
        var value = EscapeIdentifier(standalone.ResultParameter.Name);
        var valueType = GetParameterTypeName(standalone.ResultParameter);
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
        if (standalone.Diagnostics)
        {
            var tokenIndex = standalone.CancellationTokenParameter?.Ordinal ?? -1;
            var suggestions = standalone.GrammarFindings.Count > 0 ? $", Suggestions = {FormatSuggestions(standalone.GrammarFindings)}" : "";
            source.AppendLine($"        [global::Parlot.Generated.ParserDiagnosticsAttribute(1, CancellationTokenIndex = {tokenIndex}{suggestions})]");
        }
        source.AppendLine($"        {StandaloneSignature(entry)}");
        source.AppendLine("        {");
        var token = standalone.CancellationTokenParameter is { } tokenParameter ? EscapeIdentifier(tokenParameter.Name) : null;
        var cancellationArgument = token is not null ? $", {token}" : "";
        var invocation = factory.Parameters.Length > 0 ? $"{context}.Parse" : $"{wrapper}.{core}";
        var scanner = $"new global::Parlot.Scanner({input})";
        if (standalone.IsReader)
        {
            var refill = prefix + "_source";
            var isFinal = prefix + "_isFinal";
            source.AppendLine($"            if ({input} is null) throw new global::System.ArgumentNullException(nameof({input}));");
            source.AppendLine($"            using var {refill} = new global::Parlot.Fluent.TextReaderRefillSource({input});");
            source.AppendLine($"            var {prefix}_text = {refill}.ReadFirst({token ?? "default"}, out var {isFinal});");
            scanner = $"new global::Parlot.Scanner({prefix}_text, {isFinal})";
        }
        var contextCreation = factory.Parameters.Length > 0
            ? $"new {wrapper}({scanner}{cancellationArgument}, {arguments})"
            : wrapperIsContext
                ? $"new {wrapper}({scanner}{cancellationArgument})"
                : $"new global::Parlot.Fluent.ParseContext({scanner}{cancellationArgument})";
        source.AppendLine($"            var {context} = {contextCreation};");
        var diagnostics = standalone.DiagnosticsParameter is { } diagnosticsParameter ? EscapeIdentifier(diagnosticsParameter.Name) : null;
        var diagnosticType = standalone.DiagnosticsParameter is { Type: INamedTypeSymbol { TypeArguments: [var element] } }
            ? element.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
                SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier))
            : null;
        if (diagnostics is not null)
        {
            source.AppendLine($"            {context}.CollectDiagnostics = true;");
            source.AppendLine($"            {diagnostics} = global::System.Array.Empty<{diagnosticType}>();");
        }
        var failure = standalone.FailureParameter is { } failureParameter ? EscapeIdentifier(failureParameter.Name) : null;
        source.AppendLine($"            var {result} = new global::Parlot.ParseResult<{valueType}>();");
        source.AppendLine("            try");
        source.AppendLine("            {");
        if (standalone.IsReader)
        {
            var success = prefix + "_success";
            source.AppendLine($"                if (!{prefix}_isFinal) {context}.StartCompacting({prefix}_source);");
            source.AppendLine($"                var {success} = {invocation}({context}, ref {result});");
            source.AppendLine($"                {context}.CheckCompactingEnd({success});");
            source.AppendLine($"                if ({success})");
        }
        else
        {
            source.AppendLine($"                if ({invocation}({context}, ref {result}))");
        }
        source.AppendLine("                {");
        source.AppendLine($"                    {value} = {result}.Value;");
        if (failure is not null)
        {
            source.AppendLine($"                    {failure} = default;");
        }
        source.AppendLine("                    return true;");
        source.AppendLine("                }");
        if (failure is not null)
        {
            // Read while the buffer of a TextReader is still held. The parsers moved the cursor back from where they failed.
            var furthest = prefix + "_furthest";
            if (standalone.Failure == FailureOutput.Offset)
            {
                source.AppendLine($"                {failure} = {context}.Scanner.Cursor.FurthestOffset;");
            }
            else
            {
                source.AppendLine($"                var {furthest} = {context}.Scanner.Cursor.GetPosition({context}.Scanner.Cursor.FurthestOffset);");
                source.AppendLine($"                {failure} = ({furthest}.Offset, {furthest}.Line, {furthest}.Column);");
            }
        }
        source.AppendLine("            }");
        if (failure is not null)
        {
            // An Error or ElseError parser reports where it failed
            var exception = prefix + "_exception";
            source.AppendLine($"            catch (global::Parlot.ParseException {exception})");
            source.AppendLine("            {");
            source.AppendLine($"                {value} = default!;");
            source.AppendLine(standalone.Failure == FailureOutput.Offset
                ? $"                {failure} = {exception}.Position.Offset;"
                : $"                {failure} = ({exception}.Position.Offset, {exception}.Position.Line, {exception}.Position.Column);");
            source.AppendLine("                return false;");
            source.AppendLine("            }");
        }
        else
        {
            source.AppendLine("            catch (global::Parlot.ParseException)");
            source.AppendLine("            {");
            source.AppendLine($"                {value} = default!;");
            source.AppendLine("                return false;");
            source.AppendLine("            }");
        }
        if (standalone.IsReader || diagnostics is not null)
        {
            source.AppendLine("            finally");
            source.AppendLine("            {");
            if (standalone.IsReader)
            {
                source.AppendLine($"                {context}.StopCompacting();");
            }
            if (diagnostics is not null)
            {
                // The diagnostics recorded before an exception propagates are returned too
                var collected = prefix + "_collected";
                var array = prefix + "_diagnostics";
                var index = prefix + "_index";
                var item = prefix + "_item";
                source.AppendLine($"                var {collected} = {context}.Diagnostics;");
                source.AppendLine($"                if ({collected}.Count > 0)");
                source.AppendLine("                {");
                source.AppendLine($"                    var {array} = new {diagnosticType}[{collected}.Count];");
                source.AppendLine($"                    for (var {index} = 0; {index} < {array}.Length; {index}++)");
                source.AppendLine("                    {");
                source.AppendLine($"                        var {item} = {collected}[{index}];");
                source.AppendLine($"                        {array}[{index}] = ({item}.Message, {item}.IsWarning, {item}.Position.Offset, {item}.Position.Line, {item}.Position.Column);");
                source.AppendLine("                    }");
                source.AppendLine($"                    {diagnostics} = {array};");
                source.AppendLine("                }");
            }
            source.AppendLine("            }");
        }
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

    // The overloads are those of the same partial class
    private static string GenerateStandaloneDesignTimeStub(IMethodSymbol[] overloads)
    {
        var entry = overloads[0];
        var ns = entry.ContainingNamespace.IsGlobalNamespace ? "" : $"namespace {entry.ContainingNamespace.ToDisplayString()};";
        var methods = string.Join("\n", overloads.Select(static overload => $$"""
                {{StandaloneSignature(overload)}} =>
                    throw new global::System.NotSupportedException("Standalone parsers are generated during build.");
            """));
        return $$"""
            // <auto-generated />
            #nullable enable
            {{ns}}
            partial class {{EscapeIdentifier(entry.ContainingType.Name)}}
            {
            {{methods}}
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
