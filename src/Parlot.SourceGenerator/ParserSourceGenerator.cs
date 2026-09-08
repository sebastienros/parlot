using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Parlot.SourceGeneration;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace Parlot.SourceGenerator;

/// <summary>
/// Compiles build-only Parlot grammars into partial parsing methods and assembly-local support code.
/// </summary>
[Generator]
public sealed partial class ParserSourceGenerator : IIncrementalGenerator
{
    private static readonly char[] _invalidLineDirectivePathCharacters = ['\r', '\n', '\u2028', '\u2029'];
    private const int EntryCoreInliningStatementLimit = 24;

    #region Diagnostic Descriptors

    private static readonly DiagnosticDescriptor ClassNotPartialDescriptor = new(
        "PARLOT007",
        "Class must be partial",
        "Class '{0}' containing [GenerateParser] method '{1}' must be declared as partial",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Classes containing [GenerateParser] methods must be declared as partial so that the source generator can add generated code to the class.");

    private static readonly DiagnosticDescriptor MethodNotStaticDescriptor = new(
        "PARLOT008",
        "Method must be static",
        "[GenerateParser] method '{0}' must be static",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Methods marked with [GenerateParser] must be static.");

    private static readonly DiagnosticDescriptor UnsupportedFactorySignatureDescriptor = new(
        "PARLOT009",
        "Unsupported parser factory signature",
        "[GenerateParser] method '{0}' must be non-generic and use only ordinary by-value configuration parameters",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Generic factories, generic containing types, by-reference parameters, ref-like types, pointers, and function pointers are not supported.");

    private static readonly DiagnosticDescriptor InvalidFactoryParameterUseDescriptor = new(
        "PARLOT021",
        "Factory parameter must be deferred",
        "Parameter '{1}' in [GenerateParser] method '{0}' may only be read inside supported parse-time callbacks and must not be reassigned or passed by reference",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Use If or Select to describe every branch at compile time. Configuration arguments are supplied at parse time, not used to build the parser graph.");

    private static readonly DiagnosticDescriptor EagerCallbackDescriptor = new(
        "PARLOT022",
        "Captured callback executed during generation",
        "Callbacks capturing factory state in method '{0}' cannot be executed while building the parser graph",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor InvalidReturnTypeDescriptor = new(
        "PARLOT010",
        "Invalid return type",
        "[GenerateParser] method '{0}' must return Parser<T>",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Methods marked with [GenerateParser] must return a Parlot.Fluent.Parser<T>.");

    private static readonly DiagnosticDescriptor LambdaExtractionFailedDescriptor = new(
        "PARLOT003",
        "Lambda extraction failed",
        "Could not extract lambda from source for method '{0}': {1}. Define the parser inline in the [GenerateParser] method instead of referencing external static fields.",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Lambdas used in parsers must be defined inline in the [GenerateParser] method so that the source generator can extract and emit them.");

    private static readonly DiagnosticDescriptor EmitFailedDescriptor = new(
        "PARLOT001",
        "Compilation emit failed",
        "Emit failed for method '{0}': {1}",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor IncludeFileNotFoundDescriptor = new(
        "PARLOT006",
        "Include file not found",
        "[IncludeFiles] entry {0} matched no files for method '{1}'",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor InvalidIncludeFileDescriptor = new(
        "PARLOT016",
        "Invalid include file path",
        "[IncludeFiles] entry {0} for method '{1}' was rejected: {2}",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "IncludeFiles paths must be relative C# paths that remain within the project root.");

    internal static readonly DiagnosticDescriptor IncludeFileSymlinkDescriptor = new(
        "PARLOT017",
        "Include file path uses a symbolic link",
        "[IncludeFiles] entry {0} for method '{1}' was rejected because it traverses a symbolic link",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "IncludeFiles does not follow symbolic links because their targets can escape the project root.");

    internal static readonly DiagnosticDescriptor IncludeFileLimitDescriptor = new(
        "PARLOT018",
        "Include file limit exceeded",
        "[IncludeFiles] entry {0} for method '{1}' exceeded the {2} limit",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor IncludeFileReadDescriptor = new(
        "PARLOT019",
        "Include file could not be read",
        "[IncludeFiles] entry {0} for method '{1}' could not be read ({2})",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    internal static readonly DiagnosticDescriptor ProjectRootUnavailableDescriptor = new(
        "PARLOT020",
        "Project root unavailable",
        "[IncludeFiles] for method '{0}' requires the MSBuild project directory",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The Parlot build-transitive props expose MSBuildProjectDirectory to enforce project-root containment.");

    private static readonly DiagnosticDescriptor MethodNotFoundDescriptor = new(
        "PARLOT011",
        "Method not found in emitted assembly",
        "Could not find method '{0}' on type '{1}'",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ClosureNotSupportedDescriptor = new(
        "PARLOT015",
        "Unsupported lambda capture",
        "Lambda in method '{0}' captures variable '{1}' from the enclosing scope. Source generation only supports captures of that factory's parameters. Pass application state through a factory parameter instead.",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Factory parameters are supplied to generated parsing methods. Other captured locals or parameters are not supported.");

    #endregion


    /// <summary>
    /// Initializes the incremental generator to find and process methods annotated with <see cref="GenerateParserAttribute"/>.
    /// </summary>
    /// <param name="context">The generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        InitializeStandalone(context);
    }

    private static bool IsDesignTimeOrIdeContext(string? designTimeBuild, string? buildingProject, string? buildingInsideVisualStudio)
    {
        if (IsTrueProperty(designTimeBuild))
        {
            return true;
        }

        if (IsTrueProperty(buildingInsideVisualStudio)
            && TryGetBoolProperty(buildingProject, out var isBuildingProject)
            && !isBuildingProject)
        {
            return true;
        }

        return false;
    }

    private static bool IsTrueProperty(string? value)
        => TryGetBoolProperty(value, out var parsed) && parsed;

    private static bool TryGetBoolProperty(string? value, out bool parsed)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            parsed = false;
            return false;
        }

        return bool.TryParse(value, out parsed);
    }

    private static string GetMethodKey(IMethodSymbol method)
    {
        return method.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
            .WithMemberOptions(SymbolDisplayMemberOptions.IncludeContainingType
                | SymbolDisplayMemberOptions.IncludeParameters
                | SymbolDisplayMemberOptions.IncludeExplicitInterface)
            .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut)
            .WithGenericsOptions(SymbolDisplayGenericsOptions.IncludeTypeParameters));
    }


    private static bool IsCandidateMethod(SyntaxNode node)
        => node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0;

    private static MethodToGenerate? GetMethodToGenerate(GeneratorSyntaxContext context)
        => GetMethodToGenerate((MethodDeclarationSyntax)context.Node, context.SemanticModel);

    private static MethodToGenerate? GetMethodToGenerate(MethodDeclarationSyntax methodDecl, SemanticModel semanticModel)
    {
        var methodSymbol = semanticModel.GetDeclaredSymbol(methodDecl) as IMethodSymbol;
        if (methodSymbol is null)
        {
            return null;
        }

        var compilation = semanticModel.Compilation;
        var generateParserAttrSymbol = compilation.GetTypeByMetadataName("Parlot.SourceGenerator.GenerateParserAttribute");
        if (generateParserAttrSymbol is null)
        {
            return null;
        }

        // Check if method has [GenerateParser] attribute
        var hasAttribute = methodSymbol.GetAttributes()
            .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, generateParserAttrSymbol));

        if (!hasAttribute)
        {
            return null;
        }

        var attrLocation = methodSymbol.GetAttributes()
            .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, generateParserAttrSymbol))
            ?.ApplicationSyntaxReference?.GetSyntax().GetLocation();
        attrLocation = GrammarDiagnosticLocation(attrLocation);

        // Collect validation errors - we want to report them all, not just the first one
        var validationErrors = new List<DiagnosticDescriptor>();
        var validationArgs = new List<object?[]>();

        // Check if class is partial
        var containingType = methodSymbol.ContainingType;
        var typeDeclaration = containingType.DeclaringSyntaxReferences
            .Select(static reference => reference.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault(static declaration => !declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
        
        if (typeDeclaration is not null)
        {
            validationErrors.Add(ClassNotPartialDescriptor);
            validationArgs.Add(new object?[] { containingType.Name, methodSymbol.Name });
        }

        // Check if method is static
        if (!methodSymbol.IsStatic)
        {
            validationErrors.Add(MethodNotStaticDescriptor);
            validationArgs.Add(new object?[] { methodSymbol.Name });
        }

        if (methodSymbol.IsGenericMethod
            || methodSymbol.ContainingType.IsGenericType
            || methodSymbol.Parameters.Any(static parameter =>
                parameter.RefKind != RefKind.None
                || parameter.Type.IsRefLikeType
                || parameter.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer))
        {
            validationErrors.Add(UnsupportedFactorySignatureDescriptor);
            validationArgs.Add(new object?[] { methodSymbol.Name });
        }

        // Check return type
        if (!IsParserReturnType(methodSymbol.ReturnType))
        {
            validationErrors.Add(InvalidReturnTypeDescriptor);
            validationArgs.Add(new object?[] { methodSymbol.Name });
        }

        // Check for [IncludeFiles] attribute on method and containing class
        var includeFilesAttrSymbol = compilation.GetTypeByMetadataName("Parlot.SourceGenerator.IncludeFilesAttribute");
        var additionalFiles = new List<string>();
        if (includeFilesAttrSymbol is not null)
        {
            // Check class-level attribute first
            var classIncludeFilesAttr = containingType.GetAttributes()
                .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, includeFilesAttrSymbol));
            if (classIncludeFilesAttr is not null && classIncludeFilesAttr.ConstructorArguments.Length > 0)
            {
                var arg = classIncludeFilesAttr.ConstructorArguments[0];
                if (arg.Kind == TypedConstantKind.Array)
                {
                    additionalFiles.AddRange(arg.Values
                        .Where(v => v.Value is string)
                        .Select(v => (string)v.Value!));
                }
            }
            
            // Then check method-level attribute (which can add more files)
            var methodIncludeFilesAttr = methodSymbol.GetAttributes()
                .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, includeFilesAttrSymbol));
            if (methodIncludeFilesAttr is not null && methodIncludeFilesAttr.ConstructorArguments.Length > 0)
            {
                var arg = methodIncludeFilesAttr.ConstructorArguments[0];
                if (arg.Kind == TypedConstantKind.Array)
                {
                    additionalFiles.AddRange(arg.Values
                        .Where(v => v.Value is string)
                        .Select(v => (string)v.Value!));
                }
            }
        }

        // Check for [IncludeUsings] attribute on method and containing class
        var includeUsingsAttrSymbol = compilation.GetTypeByMetadataName("Parlot.SourceGenerator.IncludeUsingsAttribute");
        var additionalUsings = new List<string>();
        if (includeUsingsAttrSymbol is not null)
        {
            // Check class-level attribute first
            var classIncludeUsingsAttr = containingType.GetAttributes()
                .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, includeUsingsAttrSymbol));
            if (classIncludeUsingsAttr is not null && classIncludeUsingsAttr.ConstructorArguments.Length > 0)
            {
                var arg = classIncludeUsingsAttr.ConstructorArguments[0];
                if (arg.Kind == TypedConstantKind.Array)
                {
                    additionalUsings.AddRange(arg.Values
                        .Where(v => v.Value is string)
                        .Select(v => (string)v.Value!));
                }
            }
            
            // Then check method-level attribute (which can add more usings)
            var methodIncludeUsingsAttr = methodSymbol.GetAttributes()
                .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, includeUsingsAttrSymbol));
            if (methodIncludeUsingsAttr is not null && methodIncludeUsingsAttr.ConstructorArguments.Length > 0)
            {
                var arg = methodIncludeUsingsAttr.ConstructorArguments[0];
                if (arg.Kind == TypedConstantKind.Array)
                {
                    additionalUsings.AddRange(arg.Values
                        .Where(v => v.Value is string)
                        .Select(v => (string)v.Value!));
                }
            }
        }

        // Check for [IncludeGenerators] attribute on method and containing class
        var includeGeneratorsAttrSymbol = compilation.GetTypeByMetadataName("Parlot.SourceGenerator.IncludeGeneratorsAttribute");
        var additionalGenerators = new List<string>();
        if (includeGeneratorsAttrSymbol is not null)
        {
            // Check class-level attribute first
            var classIncludeGeneratorsAttr = containingType.GetAttributes()
                .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, includeGeneratorsAttrSymbol));
            if (classIncludeGeneratorsAttr is not null && classIncludeGeneratorsAttr.ConstructorArguments.Length > 0)
            {
                var arg = classIncludeGeneratorsAttr.ConstructorArguments[0];
                if (arg.Kind == TypedConstantKind.Array)
                {
                    additionalGenerators.AddRange(arg.Values
                        .Where(v => v.Value is string)
                        .Select(v => (string)v.Value!));
                }
            }
            
            // Then check method-level attribute (which can add more generators)
            var methodIncludeGeneratorsAttr = methodSymbol.GetAttributes()
                .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, includeGeneratorsAttrSymbol));
            if (methodIncludeGeneratorsAttr is not null && methodIncludeGeneratorsAttr.ConstructorArguments.Length > 0)
            {
                var arg = methodIncludeGeneratorsAttr.ConstructorArguments[0];
                if (arg.Kind == TypedConstantKind.Array)
                {
                    additionalGenerators.AddRange(arg.Values
                        .Where(v => v.Value is string)
                        .Select(v => (string)v.Value!));
                }
            }
        }

        return new MethodToGenerate(
            methodSymbol, 
            attrLocation, 
            additionalFiles.ToArray(), 
            additionalUsings.ToArray(),
            additionalGenerators.ToArray(),
            validationErrors.Count > 0 ? validationErrors.ToArray() : null,
            validationErrors.Count > 0 ? validationArgs.ToArray() : null);
    }

    private readonly record struct MethodToGenerate(
        IMethodSymbol Method, 
        Location? AttributeLocation, 
        string[] AdditionalFiles,
        string[] AdditionalUsings,
        string[] AdditionalGenerators,
        DiagnosticDescriptor[]? ValidationErrors,
        object?[][]? ValidationArgs);

    private static bool IsParserReturnType(ITypeSymbol returnType)
    {
        if (returnType is not INamedTypeSymbol named || !named.IsGenericType)
        {
            return false;
        }

        var constructedFrom = named.ConstructedFrom;
        var display = constructedFrom.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        // Expect "global::Parlot.Fluent.Parser<T>"
        return display == "global::Parlot.Fluent.Parser<T>";
    }

    [SuppressMessage("Build", "RS1035", Justification = "The generator must execute parser descriptors to produce source output.")]
    private static void GenerateForMethod(
        SourceProductionContext context,
        RoslynCompilation hostCompilation,
        TargetFrameworkInfo targetFramework,
        MethodToGenerate methodInfo,
        string projectDirectory,
        bool isDesignTimeBuild,
        StandaloneEntryPoint standalone,
        ISet<string> entryPointKeys)
    {
        var methodSymbol = methodInfo.Method;

        // Get the syntax tree containing this method
        var methodSyntaxRef = methodSymbol.DeclaringSyntaxReferences.FirstOrDefault();
        if (methodSyntaxRef is null)
        {
            return;
        }
        var originalSyntaxTree = methodSyntaxRef.SyntaxTree;
        var methodSyntax = methodSyntaxRef.GetSyntax() as MethodDeclarationSyntax;
        
        if (methodSyntax is null)
        {
            return;
        }

        // Get semantic model for the syntax tree
        var semanticModel = hostCompilation.GetSemanticModel(originalSyntaxTree);
        
        // Use LambdaRewriter to rewrite lambdas into stubs for execution
        // The rewriter replaces each lambda/method group with a stub that contains a unique pointer
        // When the rewritten code executes, LambdaRegistry can extract the pointer and map it
        // back to the original source code
        var invalidParameterUses = FactoryParameterUsage.FindInvalidUses(methodSymbol, methodSyntax, semanticModel).ToList();
        if (invalidParameterUses.Count > 0)
        {
            foreach (var use in invalidParameterUses)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    InvalidFactoryParameterUseDescriptor,
                    GrammarDiagnosticLocation(use.Location),
                    methodSymbol.Name,
                    use.ParameterName));
            }
            return;
        }

        var rewriter = new LambdaRewriter(semanticModel, methodSymbol);
        var rewrittenRoot = rewriter.Visit(originalSyntaxTree.GetRoot());
        
        // Create a new syntax tree with the rewritten code
        var parseOptions = (CSharpParseOptions)originalSyntaxTree.Options;
        var rewrittenTree = CSharpSyntaxTree.Create(
            (CSharpSyntaxNode)rewrittenRoot,
            parseOptions,
            originalSyntaxTree.FilePath);
        
        // Get the source code map from pointers to original lambda source
        var lambdaSourceMap = rewriter.Lambdas.ToDictionary(
            kv => kv.Key,
            kv => kv.Value.OriginalSource);

        // Replace the original syntax tree with the rewritten one in the host compilation
        // This allows us to execute the parser with lambda stubs while keeping all other files intact
        var tempCompilation = hostCompilation
            .ReplaceSyntaxTree(originalSyntaxTree, rewrittenTree);

        tempCompilation = StubStandaloneEntryPoints(tempCompilation, entryPointKeys);

        if (methodInfo.AdditionalFiles.Length > 0)
        {
            if (!IncludeFilesResolver.TryLoad(
                context,
                methodInfo.Method,
                methodInfo.AttributeLocation,
                methodInfo.AdditionalFiles,
                originalSyntaxTree,
                projectDirectory,
                parseOptions,
                tempCompilation.SyntaxTrees,
                out var includedTrees))
            {
                return;
            }

            if (includedTrees.Count > 0)
            {
                tempCompilation = tempCompilation.AddSyntaxTrees(includedTrees);
            }
        }

        // Run additional source generators if specified via [IncludeGenerators] attribute
        if (methodInfo.AdditionalGenerators.Length > 0)
        {
            tempCompilation = RunAdditionalGenerators(context, tempCompilation, methodInfo.AdditionalGenerators, parseOptions, methodSymbol, isDesignTimeBuild);
        }

        // Some generators (e.g. Logging, Regex) generate partial method implementations.
        // During this generator's execution, those implementations might not be present in the compilation snapshot.
        // If emit fails with CS8795/CS0759, rewrite the affected partial methods in-memory into non-partial stubs and retry once.
        List<string>? partialBypassedMethods = null;

        using var peStream = new System.IO.MemoryStream();
        var emitResult = tempCompilation.Emit(peStream);

        if (!emitResult.Success && emitResult.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error && (d.Id == "CS8795" || d.Id == "CS0759")))
        {
            tempCompilation = BypassPartialMethodsAndRetryEmit(tempCompilation, emitResult.Diagnostics, out partialBypassedMethods);
            peStream.SetLength(0);
            peStream.Position = 0;
            emitResult = tempCompilation.Emit(peStream);
        }

        if (!emitResult.Success)
        {
            // Check if errors are likely due to missing outputs from other source generators.
            // - CS8795: partial method missing implementation (common with LoggerMessage/GeneratedRegex)
            // - CS0246/CS0234: type/namespace not found (also common when generated types aren't present yet)
            var missingGeneratorOutputErrors = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error &&
                            (d.Id == "CS0246" || d.Id == "CS0234" || d.Id == "CS8795" || d.Id == "CS0759"))
                .ToList();
            
            // Output a diagnostic about emit failure with the error messages
            var errorMessages = string.Join("; ", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Take(5)
                .Select(d => 
                {
                    var location = d.Location;
                    var lineSpan = location.GetLineSpan();
                    var fileName = Path.GetFileName(lineSpan.Path);
                    var line = lineSpan.StartLinePosition.Line + 1;
                    return $"{fileName}({line}): {d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)}";
                }));
            
            if (missingGeneratorOutputErrors.Count > 0)
            {
                var detail = errorMessages;
                if (partialBypassedMethods is { Count: > 0 })
                {
                    detail += $" | Rewrote {partialBypassedMethods.Count} partial method(s) into stubs, but emit still failed.";
                }
                detail += " | NOTE: This commonly happens when other source generators (e.g., logging/regex) haven't produced required code in the current generator pass. A rebuild usually resolves it.";

                context.ReportDiagnostic(Diagnostic.Create(
                    EmitFailedDescriptor,
                    GrammarDiagnosticLocation(methodSymbol.Locations.FirstOrDefault()), methodSymbol.Name, detail));
            }
            else
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    EmitFailedDescriptor,
                    GrammarDiagnosticLocation(methodSymbol.Locations.FirstOrDefault()), methodSymbol.Name, errorMessages));
            }
            return;
        }

        peStream.Position = 0;

        // Build a dictionary of assembly paths from the compilation's references
        // This allows us to load assemblies that the project references
        var assemblyPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var pendingCompilationRefs = new List<(string Name, RoslynCompilation Compilation)>();
        var loadedAssemblies = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        
        // Debug: collect reference info
        var refDebugInfo = new List<string>();

        // First pass: collect all references
        foreach (var reference in hostCompilation.References)
        {
            if (reference is PortableExecutableReference peRef && peRef.FilePath is not null)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(peRef.FilePath);
                var filePath = peRef.FilePath;
                
                // If this is a ref assembly (in obj/*/ref/), try to find the actual assembly
                // Ref assemblies are metadata-only and can't be loaded at runtime
                if (filePath.Contains(Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar) ||
                    filePath.Contains("/ref/"))
                {
                    var refDirectory = Path.GetDirectoryName(filePath);
                    var intermediateDirectory = refDirectory is null ? null : Path.GetDirectoryName(refDirectory);
                    var intermediatePath = intermediateDirectory is null
                        ? null
                        : Path.Combine(intermediateDirectory, Path.GetFileName(filePath));
                    var resolvedImplementation = intermediatePath is not null && File.Exists(intermediatePath);

                    if (resolvedImplementation)
                    {
                        filePath = intermediatePath!;
                    }

                    // Try to find the actual assembly in bin folder
                    // Path like: .../obj/Debug/net10.0/ref/Samples.dll -> .../bin/Debug/net10.0/Samples.dll
                    if (!resolvedImplementation)
                    {
                        var objIndex = filePath.LastIndexOf(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                        if (objIndex < 0) objIndex = filePath.LastIndexOf("/obj/", StringComparison.OrdinalIgnoreCase);

                        if (objIndex >= 0)
                        {
                            var baseDir = filePath.Substring(0, objIndex);
                            var afterObj = filePath.Substring(objIndex + 4); // Skip "/obj"
                            var refIndex = afterObj.IndexOf(Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                            if (refIndex < 0) refIndex = afterObj.IndexOf("/ref/", StringComparison.OrdinalIgnoreCase);

                            if (refIndex >= 0)
                            {
                                var configTfm = afterObj.Substring(0, refIndex); // e.g., "/Debug/net10.0"
                                var fileName = Path.GetFileName(filePath);
                                var binPath = baseDir + Path.DirectorySeparatorChar + "bin" + configTfm + Path.DirectorySeparatorChar + fileName;

                                if (File.Exists(binPath))
                                {
                                    filePath = binPath;
                                }
                            }
                        }
                    }
                }
                
                if (!assemblyPaths.ContainsKey(assemblyName))
                {
                    assemblyPaths[assemblyName] = filePath;
                }
                refDebugInfo.Add($"PE: {assemblyName} -> {filePath}");
            }
            else if (reference is CompilationReference compRef)
            {
                // For project references, we need to emit the referenced compilation to get an assembly
                var refCompilation = compRef.Compilation;
                var refAssemblyName = refCompilation.AssemblyName;
                refDebugInfo.Add($"Compilation: {refAssemblyName}");
                if (refAssemblyName is not null && refAssemblyName != "Parlot")
                {
                    pendingCompilationRefs.Add((refAssemblyName, refCompilation));
                }
            }
            else
            {
                refDebugInfo.Add($"Unknown: {reference.GetType().Name}");
            }
        }

        // Debug info removed - assembly loading working correctly

        // Set up an assembly resolver - needs to be set up BEFORE loading CompilationReference assemblies
        // because those assemblies may have their own dependencies
        ResolveEventHandler? resolver = null;
        resolver = (sender, args) =>
        {
            var requestedName = new AssemblyName(args.Name);
            if (requestedName.Name == "Parlot")
            {
                // Return the Parlot assembly that this generator is already using
                // This ensures ISourceable and other types have the same identity
                return typeof(Parlot.Fluent.Parser<>).Assembly;
            }

            // Try to return pre-loaded assembly from project references (CompilationReference)
            if (requestedName.Name is not null && loadedAssemblies.TryGetValue(requestedName.Name, out var loadedAssembly))
            {
                return loadedAssembly;
            }

            // Try to load from file-based references (PortableExecutableReference)
            if (requestedName.Name is not null && assemblyPaths.TryGetValue(requestedName.Name, out var path))
            {
                try
                {
                    var asm = Assembly.LoadFrom(path);
                    loadedAssemblies[requestedName.Name] = asm;
                    return asm;
                }
                catch
                {
                    // Fall through to return null
                }
            }

            return null;
        };

        AppDomain.CurrentDomain.AssemblyResolve += resolver;

        // Now load CompilationReference assemblies (with resolver active)
        foreach (var (refAssemblyName, refCompilation) in pendingCompilationRefs)
        {
            if (!loadedAssemblies.ContainsKey(refAssemblyName))
            {
                using var refPeStream = new MemoryStream();
                var refEmitResult = refCompilation.Emit(refPeStream);
                if (refEmitResult.Success)
                {
                    refPeStream.Position = 0;
                    try
                    {
                        var refAssembly = Assembly.Load(refPeStream.ToArray());
                        loadedAssemblies[refAssemblyName] = refAssembly;
                    }
                    catch (Exception ex)
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            new DiagnosticDescriptor("PARLOT098", "Assembly load failed", "Failed to load '{0}': {1}", "Parlot.SourceGenerator", DiagnosticSeverity.Warning, true),
                            GrammarDiagnosticLocation(methodSymbol.Locations.FirstOrDefault()), refAssemblyName, ex.Message));
                    }
                }
                else
                {
                    var errors = string.Join("; ", refEmitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(3).Select(d => d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)));
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("PARLOT097", "Emit failed for ref", "Failed to emit '{0}': {1}", "Parlot.SourceGenerator", DiagnosticSeverity.Warning, true),
                        GrammarDiagnosticLocation(methodSymbol.Locations.FirstOrDefault()), refAssemblyName, errors));
                }
            }
        }

        try
        {
            var assembly = Assembly.Load(peStream.ToArray());

            // Locate the generated type and method via reflection
            var containingTypeName = methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            const string globalPrefix = "global::";
            if (containingTypeName.StartsWith(globalPrefix, StringComparison.Ordinal))
            {
                containingTypeName = containingTypeName.Substring(globalPrefix.Length);
            }

            var type = assembly.GetType(containingTypeName);
            if (type is null)
            {
                // This happens in the editor when the emitted assembly can't be loaded correctly
                // context.ReportDiagnostic(Diagnostic.Create(
                //     new DiagnosticDescriptor("PARLOT002", "Type not found", "Could not find type '{0}' in emitted assembly", "Parlot.SourceGenerator", DiagnosticSeverity.Warning, true),
                //     methodSymbol.Locations.FirstOrDefault(), containingTypeName));
                return;
            }

            var method = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .SingleOrDefault(candidate => MatchesFactoryMethod(candidate, methodSymbol));

            if (method is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MethodNotFoundDescriptor,
                    GrammarDiagnosticLocation(methodSymbol.Locations.FirstOrDefault()), methodSymbol.Name, containingTypeName));
                return;
            }

            // Parameters may only be read by deferred callbacks, so placeholders cannot affect the graph.
            object? parserInstance;
            LambdaPointer.Reset();
            try
            {
                var arguments = method.GetParameters()
                    .Select(static parameter => parameter.ParameterType.IsValueType
                        ? Array.CreateInstance(parameter.ParameterType, 1).GetValue(0)
                        : null)
                    .ToArray();
                parserInstance = method.Invoke(null, arguments);
            }
            catch (TargetInvocationException ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "PARLOT004",
                        "Parser factory threw",
                        "Method '{0}' threw an exception: {1}",
                        "Parlot.SourceGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    methodInfo.AttributeLocation ?? methodSymbol.Locations.FirstOrDefault(),
                    methodSymbol.Name,
                    ex.InnerException?.Message ?? ex.Message));
                return;
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "PARLOT004",
                        "Parser factory failed",
                        "Method '{0}' could not be invoked: {1}",
                        "Parlot.SourceGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    methodInfo.AttributeLocation ?? methodSymbol.Locations.FirstOrDefault(),
                    methodSymbol.Name,
                    ex.Message));
                return;
            }

            if (ReportEagerCapture(context, methodInfo))
            {
                return;
            }

            if (parserInstance is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor("PARLOT004", "Null parser", "Method '{0}' returned null", "Parlot.SourceGenerator", DiagnosticSeverity.Warning, true),
                    methodInfo.AttributeLocation ?? methodSymbol.Locations.FirstOrDefault(), methodSymbol.Name));
                return;
            }

            // The runtime type will be something like Parlot.Fluent.Then`2 or similar,
            // whose base type is Parser<T>.
            var parserType = parserInstance.GetType();
            var baseType = parserType.BaseType;
            if (baseType is null || !baseType.IsGenericType)
            {
                return;
            }

            var valueType = baseType.GetGenericArguments()[0];

            // Ensure the parser supports source generation
            // We check by interface name because the assembly loaded at runtime may have
            // a different type identity than the one referenced by the source generator
            var sourceableInterface = parserInstance.GetType().GetInterfaces()
                .FirstOrDefault(i => i.FullName == "Parlot.SourceGeneration.ISourceable");
            
            if (sourceableInterface is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "PARLOT001",
                        "Parser does not implement ISourceable",
                        "Parser type '{0}' does not implement ISourceable. Implemented interfaces: {1}",
                        "Parlot.SourceGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    methodInfo.AttributeLocation ?? methodSymbol.Locations.FirstOrDefault(),
                    parserInstance.GetType().FullName,
                    string.Join(", ", parserInstance.GetType().GetInterfaces().Select(i => i.FullName))));
                return;
            }

            // Get the GenerateSource method via reflection since we can't cast across assembly boundaries
            var generateSourceMethod = sourceableInterface.GetMethod("GenerateSource");
            if (generateSourceMethod is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "PARLOT001",
                        "ISourceable.GenerateSource not found",
                        "Parser type '{0}' implements ISourceable but GenerateSource method not found",
                        "Parlot.SourceGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    methodInfo.AttributeLocation ?? methodSymbol.Locations.FirstOrDefault(),
                    parserInstance.GetType().FullName));
                return;
            }

            static int GetCSharpLanguageMajorVersion(IMethodSymbol symbol)
            {
                var syntaxRef = symbol.DeclaringSyntaxReferences.FirstOrDefault();
                if (syntaxRef?.SyntaxTree.Options is not Microsoft.CodeAnalysis.CSharp.CSharpParseOptions csharpOptions)
                {
                    return 0;
                }

                var lv = csharpOptions.LanguageVersion;

                // Normalize common Roslyn values to a major integer.
                if (lv == Microsoft.CodeAnalysis.CSharp.LanguageVersion.Latest || lv == Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview)
                {
                    return int.MaxValue;
                }

                // Roslyn uses enum values like CSharp9, CSharp10, ...
                var name = lv.ToString();
                const string prefix = "CSharp";
                if (name.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(name.Substring(prefix.Length), out var major))
                {
                    return major;
                }

                return 0;
            }

            var sgContext = new SourceGenerationContext(
                parseContextName: "context",
                methodNamePrefix: GetGeneratedIdentifier(methodSymbol),
                targetFramework: targetFramework,
                csharpLanguageMajorVersion: GetCSharpLanguageMajorVersion(methodSymbol));
            
            // Set the lambda source map before invoking GenerateSource
            // This allows the LambdaRegistry to map runtime lambda pointers back to their original source
            sgContext.SetLambdaSourceMap(lambdaSourceMap);
            
            // Invoke GenerateSource via reflection
            object? sourceResultObj;
            try
            {
                sourceResultObj = generateSourceMethod.Invoke(parserInstance, new object[] { sgContext });
            }
            catch (TargetInvocationException ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "PARLOT005",
                        "GenerateSource threw",
                        "GenerateSource for '{0}' threw an exception: {1}",
                        "Parlot.SourceGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    methodInfo.AttributeLocation ?? methodSymbol.Locations.FirstOrDefault(),
                    parserInstance.GetType().FullName,
                    ex.InnerException?.Message ?? ex.Message));
                return;
            }

            if (sourceResultObj is not SourceResult sourceResult)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "PARLOT005",
                        "GenerateSource returned invalid result",
                        "GenerateSource for '{0}' returned '{1}' instead of SourceResult",
                        "Parlot.SourceGenerator",
                        DiagnosticSeverity.Error,
                        isEnabledByDefault: true),
                    methodInfo.AttributeLocation ?? methodSymbol.Locations.FirstOrDefault(),
                    parserInstance.GetType().FullName,
                    sourceResultObj?.GetType().FullName ?? "null"));
                return;
            }

            // Generate C# code using the new pointer-based lambda system
            var (sourceText, failedLambdas, capturedVariables) = GenerateParserCore(methodSymbol, valueType, sourceResult, sgContext, lambdaSourceMap, rewriter.Lambdas, methodInfo.AdditionalUsings, standalone);

            if (ReportEagerCapture(context, methodInfo))
            {
                return;
            }

            foreach (var captured in capturedVariables)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ClosureNotSupportedDescriptor,
                    GrammarDiagnosticLocation(captured.Location),
                    methodSymbol.Name,
                    captured.VariableName));
            }

            // Report errors for lambdas that couldn't be extracted
            foreach (var failedLambda in failedLambdas)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    LambdaExtractionFailedDescriptor,
                    GrammarDiagnosticLocation(methodSymbol.Locations.FirstOrDefault()),
                    methodSymbol.Name,
                    failedLambda));
            }

            // Only report success and add source if there were no failed lambdas
            if (failedLambdas.Count > 0 || capturedVariables.Count > 0)
            {
                return;
            }

            standalone.Source = StandaloneRuntimeSources.RewriteGeneratedSource(sourceText, parseOptions);
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolver;
        }
    }

    private static (string SourceText, List<string> FailedLambdas, List<LambdaRewriter.CapturedVariableInfo> CapturedVariables) GenerateParserCore(
        IMethodSymbol methodSymbol,
        Type valueType,
        SourceResult result,
        SourceGenerationContext sgContext,
        Dictionary<int, string> lambdaSourceMap,
        IReadOnlyDictionary<int, LambdaRewriter.LambdaInfo> lambdaInfoMap,
        string[] additionalUsings,
        StandaloneEntryPoint standalone)
    {
        var ns = methodSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : methodSymbol.ContainingNamespace.ToDisplayString();

        var typeName = EscapeIdentifier(methodSymbol.ContainingType.Name);
        var methodName = GetGeneratedIdentifier(methodSymbol);
        var valueTypeName = TypeNameHelper.GetTypeName(valueType);
        var coreName = methodName + "_Core";
        var wrapperName = "GeneratedParser_" + methodName;
        var hasParameters = methodSymbol.Parameters.Length > 0;
        var staticModifier = hasParameters ? "" : "static ";
        var parameterList = GetFactoryParameterList(methodSymbol);

        // First pass: process all deferred parsers to collect all lambdas
        // We need to do this before emitting lambda fields
        var deferredMethods = new List<(string MethodName, string ValueTypeName, SourceResult Result, string? ParserName)>();
        var processedDeferred = new HashSet<object>();
        var deferredToProcess = sgContext.Deferred.Enumerate().ToList();
        
        while (deferredToProcess.Count > 0)
        {
            var (parser, deferredMethodName, deferredParserName) = deferredToProcess[0];
            deferredToProcess.RemoveAt(0);
            
            if (processedDeferred.Contains(parser))
            {
                continue;
            }
            processedDeferred.Add(parser);

            if (parser is not ISourceable sourceable)
            {
                continue;
            }

            // Get the parser type to determine how to process it
            var parserType = parser.GetType();
            
            // Find the result type from Parser<T> base class
            Type? deferredValueType = null;
            var currentType = parserType;
            while (currentType != null)
            {
                if (currentType.IsGenericType)
                {
                    var genericDef = currentType.GetGenericTypeDefinition();
                    if (genericDef.FullName == "Parlot.Fluent.Parser`1")
                    {
                        deferredValueType = currentType.GetGenericArguments()[0];
                        break;
                    }
                }
                currentType = currentType.BaseType;
            }

            if (deferredValueType is null)
            {
                continue;
            }

            var deferredValueTypeName = TypeNameHelper.GetTypeName(deferredValueType);

            // For Deferred<T> parsers, generate source for the inner parser
            // For other parsers (like Unary<T>), generate source for the parser itself
            SourceResult deferredResult;
            if (parserType.IsGenericType && parserType.GetGenericTypeDefinition().Name == "Deferred`1")
            {
                // Get the inner parser from the Deferred
                var parserProperty = parserType.GetProperty("Parser");
                if (parserProperty is null)
                {
                    continue;
                }

                var innerParser = parserProperty.GetValue(parser);
                if (innerParser is not ISourceable innerSourceable)
                {
                    continue;
                }

                // Generate source for the inner parser
                deferredResult = innerSourceable.GenerateSource(sgContext);
                
                // If the deferred parser has no name, try to get the name from the inner parser
                if (string.IsNullOrEmpty(deferredParserName))
                {
                    var nameProp = innerParser.GetType().GetProperty("Name");
                    if (nameProp != null && nameProp.PropertyType == typeof(string))
                    {
                        deferredParserName = nameProp.GetValue(innerParser) as string;
                    }
                }
            }
            else
            {
                // For other deferred parsers (like Unary), generate source directly
                deferredResult = sourceable.GenerateSource(sgContext);
            }
            
            deferredMethods.Add((deferredMethodName, deferredValueTypeName, deferredResult, deferredParserName));
            
            // Check for new deferred parsers that were added
            foreach (var newDeferred in sgContext.Deferred.Enumerate())
            {
                if (!processedDeferred.Contains(newDeferred.Parser) && !deferredToProcess.Any(d => d.Parser == newDeferred.Parser))
                {
                    deferredToProcess.Add(newDeferred);
                }
            }
        }

        // Collect helper methods (OneOf, etc.) after all parsers/deferred have been processed
        var helperMethods = sgContext.Helpers.Enumerate().ToList();

        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable annotations");
        sb.AppendLine("#nullable disable warnings");
        
        // Default usings
        var defaultUsings = new HashSet<string>
        {
            "System",
            "System.Linq",
            "Parlot",
            "Parlot.Fluent"
        };
        
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Parlot;");
        sb.AppendLine("using Parlot.Fluent;");
        
        // Add additional usings from [IncludeUsings] attribute (excluding duplicates)
        if (additionalUsings.Length > 0)
        {
            var uniqueAdditionalUsings = additionalUsings
                .Where(u => !string.IsNullOrWhiteSpace(u) && !defaultUsings.Contains(u))
                .Distinct()
                .OrderBy(u => u);
                
            foreach (var usingNamespace in uniqueAdditionalUsings)
            {
                sb.Append("using ").Append(usingNamespace).AppendLine(";");
            }
        }
        
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine();
            sb.Append("namespace ").Append(ns).AppendLine();
            sb.AppendLine("{");
        }

        sb.AppendLine($"    partial class {typeName}");
        sb.AppendLine("    {");

        if (hasParameters)
        {
            sb.AppendLine($"        private sealed class {wrapperName} : global::Parlot.Fluent.ParseContext");
            sb.AppendLine("        {");
            foreach (var parameter in methodSymbol.Parameters)
            {
                // Mutable structs must remain addressable, just like a variable captured by a closure.
                var readOnly = parameter.Type is INamedTypeSymbol { IsValueType: true, IsReadOnly: false } ? "" : "readonly ";
                sb.AppendLine($"            private {readOnly}{GetParameterTypeName(parameter)} {FactoryParameterUsage.GetFieldName(parameter)};");
            }
            sb.AppendLine();
            var scannerParameter = "__parlotScanner";
            while (methodSymbol.Parameters.Any(parameter => parameter.Name == scannerParameter))
            {
                scannerParameter += "_";
            }
            var cancellationParameter = "__parlotCancellationToken";
            while (methodSymbol.Parameters.Any(parameter => parameter.Name == cancellationParameter))
            {
                cancellationParameter += "_";
            }
            var cancellationDeclaration = standalone.CancellationTokenParameter is not null
                ? $", global::System.Threading.CancellationToken {cancellationParameter}"
                : "";
            var cancellationArgument = standalone.CancellationTokenParameter is not null ? $", {cancellationParameter}" : "";
            sb.AppendLine($"            public {wrapperName}(global::Parlot.Scanner {scannerParameter}{cancellationDeclaration}, {parameterList}) : base({scannerParameter}{cancellationArgument})");
            sb.AppendLine("            {");
            foreach (var parameter in methodSymbol.Parameters)
            {
                sb.AppendLine($"                this.{FactoryParameterUsage.GetFieldName(parameter)} = {EscapeIdentifier(parameter.Name)};");
            }
            sb.AppendLine("            }");
            sb.AppendLine();
        }
        else
        {
            // Isolate helper names (including custom emitters) between factories in the same partial class.
            sb.AppendLine($"        private static class {wrapperName}");
            sb.AppendLine("        {");
        }

        // ==== NEW POINTER-BASED LAMBDA EMISSION ====
        // Each lambda was rewritten to a stub that captures a unique pointer.
        // The LambdaRegistry extracted these pointers during execution and mapped them to IDs.
        // Now we use the pointer to look up the original source code directly.
        
        var registeredLambdas = sgContext.Lambdas.Enumerate().OrderBy(x => x.Id).ToList();
        var failedLambdas = new List<string>();
        var capturedVariables = new List<LambdaRewriter.CapturedVariableInfo>();

        foreach (var (id, del) in registeredLambdas)
        {
            var fieldName = sgContext.GetLambdaFieldName(id);
            var delegateType = del.GetType();
            var invokeMethod = delegateType.GetMethod("Invoke");
            
            if (invokeMethod is null)
            {
                failedLambdas.Add($"Lambda {id}: could not get Invoke method");
                continue;
            }

            // Get the pointer for this lambda
            var pointer = sgContext.Lambdas.GetPointer(id);
            
            if (pointer >= 0 && lambdaSourceMap.TryGetValue(pointer, out var originalSource))
            {
                // Success! We have the original source code for this lambda.
                // Also get the lambda info for additional metadata if available.
                lambdaInfoMap.TryGetValue(pointer, out var lambdaInfo);
                var isMethodGroup = lambdaInfo?.IsMethodGroup ?? !originalSource.Contains("=>");

                if (lambdaInfo is not null && lambdaInfo.CapturedVariables.Count > 0)
                {
                    capturedVariables.AddRange(lambdaInfo.CapturedVariables);
                    continue;
                }
                
                // Generate a method from the original source with #line directive for debugging
                var methodSource = GenerateLambdaMethod(
                    fieldName, 
                    invokeMethod, 
                    originalSource, 
                    isMethodGroup,
                    lambdaInfo?.FilePath,
                    lambdaInfo?.StartLine ?? 0,
                    isStatic: !hasParameters);
                sb.AppendLine(methodSource);
            }
            else
            {
                // No pointer found - this lambda couldn't be rewritten (external lambda, etc.)
                var paramCount = invokeMethod.GetParameters().Length;
                var paramTypeNames = invokeMethod.GetParameters().Select(p => p.ParameterType.Name).ToList();
                var errorMsg = $"Lambda {id} (pointer: {pointer}, param count: {paramCount}, types: {string.Join(", ", paramTypeNames)})";
                failedLambdas.Add(errorMsg);
                
                sb.AppendLine($"        // ERROR: Lambda {id} could not be extracted from source.");
                sb.AppendLine($"        // Pointer: {pointer}, which was not found in the source map.");
                sb.AppendLine($"        // To fix: define the parser inline in the [GenerateParser] method.");
                
                // Generate a throwing method for error cases
                var errorReturnTypeName = TypeNameHelper.GetTypeName(invokeMethod.ReturnType);
                var errorParamList = GenerateParameterListWithStandardNames(invokeMethod);
                sb.AppendLine($"        private static {errorReturnTypeName} {fieldName}({errorParamList}) => throw new global::System.InvalidOperationException(\"Lambda could not be extracted from source\");");
            }
        }

        // Emit static fields registered by parsers (e.g., SearchValues<char> for AnyOf)
        if (sgContext.StaticFields.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("        // Static fields for parser data");
            foreach (var field in sgContext.StaticFields)
            {
                sb.Append("        ").AppendLine(field);
            }
        }

        if (hasParameters)
        {
            sb.AppendLine($"            public bool Parse(global::Parlot.Fluent.ParseContext context, ref global::Parlot.ParseResult<{valueTypeName}> result)");
            sb.AppendLine("            {");
            sb.AppendLine($"                return {coreName}(context, ref result);");
            sb.AppendLine("            }");
        }
        sb.AppendLine();
        // Only the entry core gets a hint. Internal helpers must retain JIT-budgeted inlining boundaries.
        if (result.Locals.Count + result.Body.Count <= EntryCoreInliningStatementLimit)
        {
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        }
        sb.AppendLine($"        internal {staticModifier}bool {coreName}(global::Parlot.Fluent.ParseContext context, ref global::Parlot.ParseResult<{valueTypeName}> result)");
        sb.AppendLine("        {");
        sb.AppendLine("            context.CheckCancellation();");
        sb.AppendLine("            var scanner = context.Scanner;");
        sb.AppendLine("            var cursor = scanner.Cursor;");
        
        // Always capture initial offset for parsers that don't skip whitespace
        if (result.ContentStartOffsetVariable == null)
        {
            sb.AppendLine("            var startOffset = cursor.Offset;");
        }
        
        sb.AppendLine();

        // Check if the parser body uses early returns (optimized literals)
        var coreBodyContainsReturn = result.Body.Any(stmt => stmt.TrimStart().StartsWith("return ", StringComparison.Ordinal));
        
        if (coreBodyContainsReturn)
        {
            // Parsers with early returns need special handling in Core:
            // 1. Declare the value variable they reference
            // 2. Convert "return true" to set result and return
            // 3. Convert "return false" to just return false
            
            sb.AppendLine($"            {valueTypeName} {result.ValueVariable} = default;");
            
            var startOffsetExpr = result.ContentStartOffsetVariable ?? "startOffset";
            
            foreach (var stmt in result.Body)
            {
                var trimmed = stmt.TrimStart();
                if (trimmed == "return true;")
                {
                    // Convert early success return to set result and return
                    sb.AppendLine($"            result = new global::Parlot.ParseResult<{valueTypeName}>({startOffsetExpr}, cursor.Offset, {result.ValueVariable});");
                    sb.AppendLine("            return true;");
                }
                else if (trimmed == "return false;")
                {
                    sb.AppendLine("            return false;");
                }
                else
                {
                    sb.Append("            ").AppendLine(stmt);
                }
            }
        }
        else
        {
            foreach (var local in result.Locals)
            {
                sb.Append("            ").AppendLine(local);
            }

            foreach (var stmt in result.Body)
            {
                sb.Append("            ").AppendLine(stmt);
            }

            sb.AppendLine();
            sb.AppendLine($"            if ({result.SuccessVariable})");
            sb.AppendLine("            {");
            
            // Use ContentStartOffsetVariable if available (for parsers that skip whitespace),
            // otherwise use the captured startOffset
            var startOffsetExpr = result.ContentStartOffsetVariable ?? "startOffset";
            
            sb.AppendLine($"                result = new global::Parlot.ParseResult<{valueTypeName}>({startOffsetExpr}, cursor.Offset, {result.ValueVariable});");
            sb.AppendLine("                return true;");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            return false;");
        }
        sb.AppendLine("        }");

        // Emit helper methods for deferred parsers (already processed earlier)
        foreach (var (deferredMethodName, deferredValueTypeName, deferredResult, deferredParserName) in deferredMethods)
        {
            sb.AppendLine();
            // Add parser name as comment if available
            if (!string.IsNullOrEmpty(deferredParserName))
            {
                sb.AppendLine($"        // {deferredParserName}");
            }
            sb.AppendLine($"        private {staticModifier}bool {deferredMethodName}(global::Parlot.Fluent.ParseContext context, out {deferredValueTypeName} value)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (context.MaxRecursionDepth > 0)");
            sb.AppendLine("            {");
            sb.AppendLine("                context.EnterRecursion();");
            sb.AppendLine();
            sb.AppendLine("                try");
            sb.AppendLine("                {");
            sb.AppendLine($"                    return {deferredMethodName}_Core(context, out value);");
            sb.AppendLine("                }");
            sb.AppendLine("                finally");
            sb.AppendLine("                {");
            sb.AppendLine("                    context.ExitRecursion();");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine($"            return {deferredMethodName}_Core(context, out value);");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine($"        private {staticModifier}bool {deferredMethodName}_Core(global::Parlot.Fluent.ParseContext context, out {deferredValueTypeName} value)");
            sb.AppendLine("        {");
            sb.AppendLine("            context.CheckCancellation();");
            sb.AppendLine("            var scanner = context.Scanner;");
            sb.AppendLine("            var cursor = scanner.Cursor;");
            sb.AppendLine();

            foreach (var local in deferredResult.Locals)
            {
                sb.Append("            ").AppendLine(local);
            }

            foreach (var stmt in deferredResult.Body)
            {
                sb.Append("            ").AppendLine(stmt);
            }

            // Only add final return if the body doesn't already contain returns (e.g., optimized literal parsers use early returns)
            var bodyContainsReturn = deferredResult.Body.Any(stmt => stmt.TrimStart().StartsWith("return ", StringComparison.Ordinal));
            if (!bodyContainsReturn)
            {
                sb.AppendLine();
                sb.AppendLine($"            value = {deferredResult.ValueVariable};");
                sb.AppendLine($"            return {deferredResult.SuccessVariable};");
            }
            sb.AppendLine("        }");
        }

        // Emit helper methods registered via ParserHelperRegistry (e.g., OneOf buckets)
        foreach (var (helperMethodName, helperValueTypeName, helperResult, helperParserName) in helperMethods)
        {
            sb.AppendLine();
            // Add parser name as comment if available
            if (!string.IsNullOrEmpty(helperParserName))
            {
                sb.AppendLine($"        // {helperParserName}");
            }
            var implementationName = helperMethodName;
            if (hasParameters)
            {
                // Static whitespace adapters recover this call's state from the existing execution context.
                implementationName += "_Core";
                sb.AppendLine($"        private static bool {helperMethodName}(global::Parlot.Fluent.ParseContext context, out {helperValueTypeName} value)");
                sb.AppendLine("        {");
                sb.AppendLine($"            return (({wrapperName})context).{implementationName}(context, out value);");
                sb.AppendLine("        }");
            }
            sb.AppendLine($"        private {staticModifier}bool {implementationName}(global::Parlot.Fluent.ParseContext context, out {helperValueTypeName} value)");
            sb.AppendLine("        {");
            sb.AppendLine("            context.CheckCancellation();");
            sb.AppendLine("            var scanner = context.Scanner;");
            sb.AppendLine("            var cursor = scanner.Cursor;");
            sb.AppendLine();

            foreach (var local in helperResult.Locals)
            {
                sb.Append("            ").AppendLine(local);
            }

            foreach (var stmt in helperResult.Body)
            {
                sb.Append("            ").AppendLine(stmt);
            }

            // Only add final return if the body doesn't already contain returns (e.g., optimized literal parsers use early returns)
            var bodyContainsReturn = helperResult.Body.Any(stmt => stmt.TrimStart().StartsWith("return ", StringComparison.Ordinal));
            if (!bodyContainsReturn)
            {
                sb.AppendLine();
            sb.AppendLine($"            value = {helperResult.ValueVariable};");
            sb.AppendLine($"            return {helperResult.SuccessVariable};");
            }
            sb.AppendLine("        }");
        }
        sb.AppendLine();

        sb.AppendLine("        }");
        sb.AppendLine();

        AppendStandaloneEntryPoint(sb, standalone, wrapperName, coreName);

        // Generate helper methods if needed (e.g., CreateCharMap for ListOfChars on netstandard)
        // Note: Currently no helper methods are needed since we use HashSet<char> and SearchValues<char>
        // which are both public types.

        sb.AppendLine("    }");

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine("}");
        }

        return (sb.ToString(), failedLambdas, capturedVariables);
    }

    private static string GetGeneratedIdentifier(IMethodSymbol method)
    {
        var identifier = $"__Parlot_{method.Name.Length}_{method.Name}";
        var overloads = method.ContainingType.GetMembers(method.Name).OfType<IMethodSymbol>()
            .OrderBy(GetMethodKey, StringComparer.Ordinal).ToArray();
        if (overloads.Length > 1)
        {
            identifier += "_Overload" + Array.FindIndex(overloads, candidate =>
                SymbolEqualityComparer.Default.Equals(candidate, method)).ToString(CultureInfo.InvariantCulture);
        }
        var candidate = identifier;
        var suffix = 0;

        while (HasGeneratedMemberCollision(method.ContainingType, candidate))
        {
            candidate = identifier + "_" + (++suffix).ToString(CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    private static string GetFactoryParameterList(IMethodSymbol method)
        => string.Join(", ", method.Parameters.Select(static parameter =>
            $"{GetParameterTypeName(parameter)} {EscapeIdentifier(parameter.Name)}"));

    private static bool ReportEagerCapture(SourceProductionContext context, MethodToGenerate method)
    {
        if (!LambdaPointer.HasEagerCapture)
        {
            return false;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            EagerCallbackDescriptor,
            method.AttributeLocation ?? method.Method.Locations.FirstOrDefault(),
            method.Method.Name));
        return true;
    }

    private static string GetParameterTypeName(IParameterSymbol parameter)
        => parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayFormat.FullyQualifiedFormat.MiscellaneousOptions | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier));

    private static bool MatchesFactoryMethod(MethodInfo candidate, IMethodSymbol method)
    {
        if (candidate.Name != method.MetadataName || candidate.IsGenericMethod)
        {
            return false;
        }

        var parameters = candidate.GetParameters();
        return parameters.Length == method.Parameters.Length
            && parameters.Select((parameter, index) => MatchesParameterType(parameter.ParameterType, method.Parameters[index].Type)).All(static match => match);
    }

    private static bool MatchesParameterType(Type type, ITypeSymbol symbol)
    {
        if (symbol is IArrayTypeSymbol array)
        {
            return type.IsArray && type.GetArrayRank() == array.Rank && MatchesParameterType(type.GetElementType()!, array.ElementType);
        }

        if (symbol is IDynamicTypeSymbol)
        {
            return type == typeof(object);
        }

        if (symbol is not INamedTypeSymbol named)
        {
            return false;
        }

        if (named.IsTupleType)
        {
            named = named.TupleUnderlyingType!;
        }

        var typeNames = new Stack<string>();
        var typeArguments = new List<ITypeSymbol>();
        for (var current = named; current is not null; current = current.ContainingType)
        {
            typeNames.Push(current.MetadataName);
            typeArguments.InsertRange(0, current.TypeArguments);
        }

        var namespaceName = named.ContainingNamespace.IsGlobalNamespace ? "" : named.ContainingNamespace.ToDisplayString() + ".";
        var metadataName = namespaceName + string.Join("+", typeNames);
        var definition = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
        return definition.FullName == metadataName
            && (!type.IsGenericType || type.GetGenericArguments()
                .Select((argument, index) => MatchesParameterType(argument, typeArguments[index])).All(static match => match));
    }

    private static bool HasGeneratedMemberCollision(INamedTypeSymbol containingType, string identifier)
    {
        var generatedPrefix = identifier + "_";
        var generatedFieldPrefix = "_" + generatedPrefix;
        var wrapperName = "GeneratedParser_" + identifier;
        var parserFieldName = "_generated_" + identifier;

        foreach (var member in containingType.GetMembers())
        {
            var name = member.Name;
            if (name == identifier
                || name.StartsWith(generatedPrefix, StringComparison.Ordinal)
                || name.StartsWith(generatedFieldPrefix, StringComparison.Ordinal)
                || name == wrapperName
                || name == parserFieldName)
            {
                return true;
            }
        }

        return false;
    }

    private static string EscapeIdentifier(string identifier)
        => IsReservedKeyword(identifier) ? "@" + identifier : identifier;

    private static bool IsReservedKeyword(string identifier)
        => SyntaxFacts.GetKeywordKind(identifier) != SyntaxKind.None;

    /// <summary>
    /// Generates a method from a lambda source or method group.
    /// Transforms "static x => x.ToUpper()" into "private static string MethodName(TextSpan x) => x.ToUpper();"
    /// Transforms "char.IsLetterOrDigit" into "private static bool MethodName(char arg0) => char.IsLetterOrDigit(arg0);"
    /// 
    /// For lambdas, preserves original parameter names and body verbatim to enable proper debugging.
    /// Emits #line directives to map breakpoints back to the original source location.
    /// </summary>
    private static string GenerateLambdaMethod(
        string methodName, 
        System.Reflection.MethodInfo invokeMethod, 
        string lambdaSource, 
        bool isMethodGroup,
        string? originalFilePath,
        int originalLine,
        bool isStatic)
    {
        var sb = new StringBuilder();
        var staticModifier = isStatic ? "static " : "";
        var returnTypeName = TypeNameHelper.GetTypeName(invokeMethod.ReturnType);
        var parameters = invokeMethod.GetParameters();

        // Emit #line directive for debugging if we have location info
        var hasLineInfo = !string.IsNullOrEmpty(originalFilePath)
            && originalLine > 0
            && CanEmitLineDirective(originalFilePath!);
        
        if (isMethodGroup)
        {
            // For method groups like "char.IsLetterOrDigit", generate a call with standard param names
            // private static bool _lambda0(char arg0) => char.IsLetterOrDigit(arg0);
            var paramList = GenerateParameterListWithStandardNames(invokeMethod);
            
            if (hasLineInfo)
            {
                AppendLineDirective(sb, originalLine, originalFilePath!);
            }
            
            sb.Append($"        private {staticModifier}{returnTypeName} {methodName}({paramList}) => ");
            sb.Append(lambdaSource);
            sb.Append('(');
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(GetParameterName(i));
            }
            sb.Append(");");
            
            if (hasLineInfo)
            {
                sb.Append("\n#line default");
            }
        }
        else
        {
            if (SyntaxFactory.ParseExpression(lambdaSource) is not LambdaExpressionSyntax lambda || lambda.ContainsDiagnostics)
            {
                throw new NotSupportedException("The callback could not be parsed as a C# lambda.");
            }

            var originalParamNames = lambda switch
            {
                SimpleLambdaExpressionSyntax simple => new[] { simple.Parameter.Identifier.Text },
                ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters
                    .Select(static parameter => parameter.Identifier.Text).ToArray(),
                _ => throw new NotSupportedException("Unsupported lambda syntax.")
            };
            var paramList = GenerateParameterListWithOriginalNames(invokeMethod, originalParamNames);
            var body = lambda.Body.ToFullString().Trim();
            var asyncModifier = lambda.Modifiers.Any(SyntaxKind.AsyncKeyword) ? "async " : "";

            if (lambda.Body is BlockSyntax)
            {
                sb.Append($"        private {staticModifier}{asyncModifier}{returnTypeName} {methodName}({paramList})\n");
                if (hasLineInfo)
                {
                    sb.Append("        ");
                    sb.Append(AddLineDirectivesToBody(body, originalLine, originalFilePath!));
                    sb.Append("\n#line default");
                }
                else
                {
                    sb.Append("        ");
                    sb.Append(body);
                }
            }
            else
            {
                if (hasLineInfo)
                {
                    AppendLineDirective(sb, originalLine, originalFilePath!);
                }
                sb.Append($"        private {staticModifier}{asyncModifier}{returnTypeName} {methodName}({paramList}) => ");
                sb.Append(body);
                sb.Append(';');
                if (hasLineInfo)
                {
                    sb.Append("\n#line default");
                }
            }
        }
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates a parameter list using original parameter names from the lambda.
    /// For "x => x.Length" generates "TextSpan x".
    /// </summary>
    private static string GenerateParameterListWithOriginalNames(System.Reflection.MethodInfo invokeMethod, string[] originalNames)
    {
        var parameters = invokeMethod.GetParameters();
        var sb = new StringBuilder();
        
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            var paramTypeName = TypeNameHelper.GetTypeName(parameters[i].ParameterType);
            sb.Append(paramTypeName);
            sb.Append(' ');
            // Use original name if available, otherwise fall back to standard name
            sb.Append(i < originalNames.Length ? originalNames[i] : GetParameterName(i));
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates a parameter list with standard names like "TextSpan arg0, char arg1".
    /// Used for method groups and fallback cases.
    /// </summary>
    private static string GenerateParameterListWithStandardNames(System.Reflection.MethodInfo invokeMethod)
    {
        var parameters = invokeMethod.GetParameters();
        var sb = new StringBuilder();
        
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            var paramTypeName = TypeNameHelper.GetTypeName(parameters[i].ParameterType);
            sb.Append(paramTypeName);
            sb.Append(' ');
            sb.Append(GetParameterName(i));
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Gets a standard parameter name for index i.
    /// </summary>
    private static string GetParameterName(int index) => index == 0 ? "arg0" : $"arg{index}";

    /// <summary>
    /// Adds #line directives before each line of a lambda body to enable debugging on any line.
    /// </summary>
    private static string AddLineDirectivesToBody(string body, int startLine, string filePath)
    {
        var lines = body.Split('\n');
        if (lines.Length <= 1)
        {
            // Single line - just add one directive
            return $"#line {startLine} {LiteralHelper.StringToLiteral(filePath)}\n{body}";
        }

        var sb = new StringBuilder();
        var currentLine = startLine;
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmedLine = line.TrimStart();
            
            // Only add #line directive for non-empty lines that contain actual code
            // Skip adding directives for lines that are just whitespace or closing braces
            var isSignificantLine = !string.IsNullOrWhiteSpace(trimmedLine) && 
                                    trimmedLine != "{" && 
                                    trimmedLine != "}";
            
            if (isSignificantLine)
            {
                AppendLineDirective(sb, currentLine, filePath);
            }
            
            sb.Append(line);
            
            if (i < lines.Length - 1)
            {
                sb.Append('\n');
            }
            
            // Increment line number for each line in the original source
            currentLine++;
        }
        
        return sb.ToString();
    }

    private static void AppendLineDirective(StringBuilder builder, int line, string filePath)
        => builder
            .Append("#line ")
            .Append(line)
            .Append(' ')
            .Append(LiteralHelper.StringToLiteral(filePath))
            .Append('\n');

    private static bool CanEmitLineDirective(string filePath)
        => filePath.IndexOfAny(_invalidLineDirectivePathCharacters) < 0;

    /// <summary>
    /// Runs additional source generators specified via [IncludeGenerators] attribute on the compilation.
    /// This allows the compilation to include outputs from generators like PolySharp polyfills before emitting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>IDE/Design-Time Limitation:</strong> This method only runs during actual builds, not in 
    /// design-time builds (IDE IntelliSense, code analysis). When running in design-time contexts, 
    /// dynamically loading generator assemblies via Assembly.LoadFrom can cause assembly version conflicts.
    /// </para>
    /// <para>
    /// For example, a generator like PolySharp may be compiled against Microsoft.CodeAnalysis 4.3.0, but 
    /// Visual Studio hosts Roslyn with a different version (e.g., 4.8.0 or newer). When attempting to load 
    /// the generator assembly, .NET's assembly resolution fails because:
    /// <list type="bullet">
    ///   <item>The exact referenced version isn't available in the IDE's hosting context</item>
    ///   <item>Assembly binding redirects from the project don't apply in the IDE process</item>
    ///   <item>The AppDomain lacks the necessary fusion context to resolve version mismatches</item>
    /// </list>
    /// </para>
    /// <para>
    /// This results in errors like: "Could not load file or assembly 'Microsoft.CodeAnalysis, Version=4.3.0.0'".
    /// </para>
    /// <para>
    /// <strong>Workaround:</strong> IntelliSense and error reporting still work correctly because the IDE 
    /// runs the actual referenced generators normally. The [IncludeGenerators] feature only affects the 
    /// internal compilation used by Parlot's source generator to emit code, so skipping it in design-time 
    /// builds has no user-visible impact on the editing experience.
    /// </para>
    /// </remarks>
    [SuppressMessage("Build", "RS1035", Justification = "Loading generator assemblies is necessary to run them.")]
    private static RoslynCompilation RunAdditionalGenerators(
        SourceProductionContext context,
        RoslynCompilation compilation,
        string[] generatorAssemblyNames,
        CSharpParseOptions parseOptions,
        IMethodSymbol methodSymbol,
        bool isDesignTimeBuild)
    {
        // Skip running additional generators in design-time builds to avoid assembly loading conflicts.
        // During design-time builds (IDE IntelliSense, code analysis), MSBuild sets DesignTimeBuild=true.
        // Dynamically loading generator assemblies compiled against different Microsoft.CodeAnalysis 
        // versions causes "Could not load file or assembly" errors in these contexts.
        // See the XML remarks on this method for details.
        if (isDesignTimeBuild)
        {
            // In design-time build - skip loading external generators to avoid assembly version conflicts.
            // The user's project still gets full IntelliSense from the actual referenced generators.
            return compilation;
        }

        // Ensure we have a CSharpCompilation
        if (compilation is not CSharpCompilation csharpCompilation)
        {
            return compilation;
        }

        var generators = new List<ISourceGenerator>();
        var loadedAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Build a map of analyzer assembly paths from the compilation's references
        var analyzerPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var reference in csharpCompilation.References)
        {
            if (reference is PortableExecutableReference peRef && peRef.FilePath is not null)
            {
                var assemblyName = Path.GetFileNameWithoutExtension(peRef.FilePath);
                
                // Check if this might be an analyzer/generator assembly
                // Analyzers are typically referenced from packages/analyzers folders
                var filePath = peRef.FilePath;
                if (filePath.Contains("analyzers") || filePath.Contains("Analyzers"))
                {
                    if (!analyzerPaths.ContainsKey(assemblyName))
                    {
                        analyzerPaths[assemblyName] = filePath;
                    }
                }
            }
        }

        // Also look for analyzer paths in common NuGet package locations
        // Try to find analyzer assemblies from the project's NuGet packages
        var searchPaths = new List<string>();
        
        // Add common NuGet package cache locations
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            searchPaths.Add(Path.Combine(userProfile, ".nuget", "packages"));
        }

        foreach (var generatorName in generatorAssemblyNames)
        {
            if (loadedAssemblyNames.Contains(generatorName))
            {
                continue;
            }

            string? assemblyPath = null;

            // First check if we already found it in the compilation references
            if (analyzerPaths.TryGetValue(generatorName, out var knownPath))
            {
                assemblyPath = knownPath;
            }
            else
            {
                // Search in NuGet package folders
                foreach (var searchPath in searchPaths)
                {
                    if (!Directory.Exists(searchPath))
                    {
                        continue;
                    }

                    // Look for package folder matching the generator name (case-insensitive)
                    var packageDir = Path.Combine(searchPath, generatorName.ToLowerInvariant());
                    if (Directory.Exists(packageDir))
                    {
                        // Find the latest version folder
                        var versionDirs = Directory.GetDirectories(packageDir)
                            .OrderByDescending(d => d)
                            .ToArray();

                        foreach (var versionDir in versionDirs)
                        {
                            // Look for analyzer assemblies
                            var analyzerDir = Path.Combine(versionDir, "analyzers", "dotnet", "cs");
                            if (Directory.Exists(analyzerDir))
                            {
                                var dllPath = Path.Combine(analyzerDir, generatorName + ".dll");
                                if (File.Exists(dllPath))
                                {
                                    assemblyPath = dllPath;
                                    break;
                                }

                                // Try to find any .dll in the analyzer folder
                                var dlls = Directory.GetFiles(analyzerDir, "*.dll");
                                if (dlls.Length > 0)
                                {
                                    assemblyPath = dlls[0];
                                    break;
                                }
                            }
                        }
                    }

                    if (assemblyPath is not null)
                    {
                        break;
                    }
                }
            }

            if (assemblyPath is null || !File.Exists(assemblyPath))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "PARLOT012",
                        "Generator assembly not found",
                        "Could not find generator assembly '{0}' specified in [IncludeGenerators]. Make sure the package is referenced in your project.",
                        "Parlot.SourceGenerator",
                        DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    methodSymbol.Locations.FirstOrDefault(),
                    generatorName));
                continue;
            }

            try
            {
                var assembly = Assembly.LoadFrom(assemblyPath);
                loadedAssemblyNames.Add(generatorName);

                // Find all source generator types in the assembly
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface)
                    {
                        continue;
                    }

                    // Check for IIncrementalGenerator
                    if (typeof(IIncrementalGenerator).IsAssignableFrom(type))
                    {
                        try
                        {
                            var instance = (IIncrementalGenerator)Activator.CreateInstance(type)!;
                            generators.Add(instance.AsSourceGenerator());
                        }
                        catch
                        {
                            // Skip types that can't be instantiated
                        }
                    }
                    // Check for ISourceGenerator (legacy generators)
                    else if (typeof(ISourceGenerator).IsAssignableFrom(type))
                    {
                        try
                        {
                            var instance = (ISourceGenerator)Activator.CreateInstance(type)!;
                            generators.Add(instance);
                        }
                        catch
                        {
                            // Skip types that can't be instantiated
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "PARLOT013",
                        "Failed to load generator assembly",
                        "Failed to load generator assembly '{0}': {1}",
                        "Parlot.SourceGenerator",
                        DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    methodSymbol.Locations.FirstOrDefault(),
                    generatorName,
                    ex.Message));
            }
        }

        if (generators.Count == 0)
        {
            return csharpCompilation;
        }

        // Run the generators
        try
        {
            var driver = CSharpGeneratorDriver.Create(generators, parseOptions: parseOptions);
            driver.RunGeneratorsAndUpdateCompilation(csharpCompilation, out var updatedCompilation, out var generatorDiagnostics);

            // Report any errors from the generators (but not warnings/info to avoid noise)
            foreach (var diag in generatorDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                context.ReportDiagnostic(diag);
            }

            return (CSharpCompilation)updatedCompilation;
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "PARLOT014",
                    "Failed to run generators",
                    "Failed to run additional generators: {0}",
                    "Parlot.SourceGenerator",
                    DiagnosticSeverity.Warning,
                    isEnabledByDefault: true),
                methodSymbol.Locations.FirstOrDefault(),
                ex.Message));

            return csharpCompilation;
        }
    }

    private static RoslynCompilation BypassPartialMethodsAndRetryEmit(
        RoslynCompilation compilation,
        ImmutableArray<Diagnostic> diagnostics,
        out List<string> bypassedMethods)
    {
        bypassedMethods = new List<string>();

        var targetsByTree = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error && (d.Id == "CS8795" || d.Id == "CS0759"))
            .Where(d => d.Location.SourceTree is not null)
            .GroupBy(d => d.Location.SourceTree!, d => d.Location.SourceSpan)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var kv in targetsByTree)
        {
            var tree = kv.Key;
            var spans = kv.Value;
            var root = tree.GetRoot();
            var rewriter = new PartialMethodBypassRewriter(spans, bypassedMethods);
            var newRoot = rewriter.Visit(root);

            if (!ReferenceEquals(root, newRoot) && newRoot is CSharpSyntaxNode csharp)
            {
                var newTree = CSharpSyntaxTree.Create(
                    csharp,
                    (CSharpParseOptions)tree.Options,
                    tree.FilePath);

                compilation = compilation.ReplaceSyntaxTree(tree, newTree);
            }
        }

        return compilation;
    }

    private sealed class PartialMethodBypassRewriter : CSharpSyntaxRewriter
    {
        private readonly List<Microsoft.CodeAnalysis.Text.TextSpan> _targetSpans;
        private readonly List<string> _bypassedMethods;

        public PartialMethodBypassRewriter(List<Microsoft.CodeAnalysis.Text.TextSpan> targetSpans, List<string> bypassedMethods)
        {
            _targetSpans = targetSpans;
            _bypassedMethods = bypassedMethods;
        }

        public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (!_targetSpans.Any(s => Overlaps(s, node.Span)))
            {
                return base.VisitMethodDeclaration(node);
            }

            // Only partial methods.
            if (!node.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                return base.VisitMethodDeclaration(node);
            }

            // Convert to a normal method by removing the 'partial' modifier and providing a stub body.
            // This avoids both:
            // - CS8795 (definition requires implementation)
            // - CS0759 (implementation has no defining declaration)
            var newModifiers = SyntaxFactory.TokenList(node.Modifiers.Where(m => !m.IsKind(SyntaxKind.PartialKeyword)));

            // If there is already a body/expression body, keep it (just remove 'partial').
            if (node.Body is not null || node.ExpressionBody is not null)
            {
                _bypassedMethods.Add(node.Identifier.Text);
                return node.WithModifiers(newModifiers);
            }

            // Otherwise, add a stub body.
            if (node.ReturnType is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
            {
                _bypassedMethods.Add(node.Identifier.Text);
                return node
                    .WithModifiers(newModifiers)
                    .WithBody(SyntaxFactory.Block())
                    .WithSemicolonToken(default);
            }

            _bypassedMethods.Add(node.Identifier.Text);

            var defaultExpr = SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression);
            var exprBody = SyntaxFactory.ArrowExpressionClause(defaultExpr);
            return node
                .WithModifiers(newModifiers)
                .WithExpressionBody(exprBody)
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        }

        private static bool Overlaps(Microsoft.CodeAnalysis.Text.TextSpan a, Microsoft.CodeAnalysis.Text.TextSpan b)
            => a.Start < b.End && b.Start < a.End;
    }
}
