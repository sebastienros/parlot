using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
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
/// Incremental source generator for Parlot grammars using C# interceptors.
///
/// It looks for static methods annotated with <see cref="GenerateParserAttribute"/> that:
///   - are static
///   - return Parlot.Fluent.Parser&lt;T&gt;
/// It then:
///   - finds all invocation sites of these methods,
///   - builds a temporary compilation including those methods,
///   - executes the descriptor methods to obtain Parser&lt;T&gt; instances,
///   - invokes ISourceable.GenerateSource on those instances,
///   - and emits interceptor methods that return the source-generated Parser&lt;T&gt;.
/// </summary>
[Generator]
public sealed class ParserSourceGenerator : IIncrementalGenerator
{
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

    private static readonly DiagnosticDescriptor MethodHasParametersDescriptor = new(
        "PARLOT009",
        "Method must be parameterless",
        "[GenerateParser] method '{0}' must not have parameters",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Methods marked with [GenerateParser] must be parameterless.");

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

    private static readonly DiagnosticDescriptor GenerationSucceededDescriptor = new(
        "PARLOT000",
        "Parser source generation succeeded",
        "Successfully generated source for parser '{0}' with {1} intercepted call site(s)",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "The source generator successfully generated code for this parser.");

    private static readonly DiagnosticDescriptor EmitFailedDescriptor = new(
        "PARLOT001",
        "Compilation emit failed",
        "Emit failed for method '{0}': {1}",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor GenerationErrorDescriptor = new(
        "PARLOT005",
        "Error generating parser source",
        "An error occurred while generating parser source for method '{0}': {1}",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor IncludeFileNotFoundDescriptor = new(
        "PARLOT006",
        "Include file not found",
        "Could not find included file '{0}' for method '{1}'",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor MethodNotFoundDescriptor = new(
        "PARLOT011",
        "Method not found in emitted assembly",
        "Could not find method '{0}' on type '{1}'",
        "Parlot.SourceGenerator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    #endregion

    /// <summary>
    /// Initializes the incremental generator to find and process methods annotated with <see cref="GenerateParserAttribute"/>.
    /// </summary>
    /// <param name="context">The generator initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Find candidate methods syntactically (methods with attributes).
        var methodDeclarations = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => IsCandidateMethod(node),
            static (syntaxContext, _) => GetMethodToGenerate(syntaxContext))
            .Where(static m => m is not null)!;

        // 2. Find invocations of methods (potential calls to [GenerateParser] methods).
        var invocations = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is InvocationExpressionSyntax,
            static (syntaxContext, ct) => GetInvocationToIntercept(syntaxContext, ct))
            .Where(static i => i is not null)!;

        // 3. Combine the collected methods with invocations and Compilation.
        var combinedData = context.CompilationProvider
            .Combine(methodDeclarations.Collect())
            .Combine(invocations.Collect());

        // 4. Register for source output.
        context.RegisterSourceOutput(combinedData, static (spc, source) =>
        {
            var ((compilation, methods), invocationList) = source;
            
            // Always output a debug file to confirm this runs
            spc.AddSource("ParlotDebugInfo.g.cs", SourceText.From(
                "// <auto-generated />\n" +
                "// Methods count: " + (methods.IsDefaultOrEmpty ? "0" : methods.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)) + "\n" +
                "// Invocations count: " + (invocationList.IsDefaultOrEmpty ? "0" : invocationList.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)) + "\n" +
                "namespace Parlot.SourceGenerator.Internal;\n" +
                "internal static class DebugInfo { }\n", Encoding.UTF8));

            if (methods.IsDefaultOrEmpty)
            {
                return;
            }

            // Build a lookup of method symbols to their invocations
            var invocationsByMethod = new Dictionary<string, List<InvocationInfo>>(StringComparer.Ordinal);
            if (!invocationList.IsDefaultOrEmpty)
            {
                foreach (var inv in invocationList)
                {
                    if (inv is null) continue;
                    var key = inv.Value.TargetMethodKey;
                    if (!invocationsByMethod.TryGetValue(key, out var list))
                    {
                        list = new List<InvocationInfo>();
                        invocationsByMethod[key] = list;
                    }
                    list.Add(inv.Value);
                }
            }

            foreach (var m in methods)
            {
                if (m is null)
                {
                    continue;
                }

                // Report any validation errors first
                if (m.Value.ValidationErrors is not null && m.Value.ValidationArgs is not null)
                {
                    for (int i = 0; i < m.Value.ValidationErrors.Length; i++)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            m.Value.ValidationErrors[i],
                            m.Value.AttributeLocation ?? m.Value.Method.Locations.FirstOrDefault(),
                            m.Value.ValidationArgs[i]));
                    }
                    // Skip generation if there are validation errors
                    continue;
                }

                try
                {
                    // Get invocations for this method
                    var methodKey = GetMethodKey(m.Value.Method);
                    invocationsByMethod.TryGetValue(methodKey, out var methodInvocations);
                    
                    GenerateForMethod(spc, compilation, m.Value, methodInvocations ?? new List<InvocationInfo>());
                }
                catch (Exception ex)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        GenerationErrorDescriptor,
                        m.Value.Method.Locations.FirstOrDefault(),
                        m.Value.Method.Name,
                        ex.Message));
                    continue;
                }
            }
        });
    }

    private static string GetMethodKey(IMethodSymbol method)
    {
        return method.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + method.Name;
    }

    private static InvocationInfo? GetInvocationToIntercept(GeneratorSyntaxContext context, System.Threading.CancellationToken ct)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        
        // Get the method being called
        var symbolInfo = semanticModel.GetSymbolInfo(invocation, ct);
        if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
        {
            return null;
        }

        // Check if the method has [GenerateParser] attribute
        var generateParserAttrSymbol = semanticModel.Compilation.GetTypeByMetadataName("Parlot.SourceGenerator.GenerateParserAttribute");
        if (generateParserAttrSymbol is null)
        {
            return null;
        }

        var hasGenerateParserAttr = methodSymbol.GetAttributes()
            .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, generateParserAttrSymbol));
        
        if (!hasGenerateParserAttr)
        {
            return null;
        }

        // Only intercept parameterless method calls
        if (invocation.ArgumentList.Arguments.Count > 0)
        {
            return null;
        }

        // Get the interceptable location using Roslyn's API
        var interceptableLocation = semanticModel.GetInterceptableLocation(invocation, ct);
        if (interceptableLocation is null)
        {
            return null;
        }

        var methodKey = methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + methodSymbol.Name;
        
        return new InvocationInfo(
            methodKey,
            interceptableLocation.GetInterceptsLocationAttributeSyntax(),
            invocation.GetLocation());
    }

    private static bool IsCandidateMethod(SyntaxNode node)
        => node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0;

    private static MethodToGenerate? GetMethodToGenerate(GeneratorSyntaxContext context)
    {
        var methodDecl = (MethodDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
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

        // Collect validation errors - we want to report them all, not just the first one
        var validationErrors = new List<DiagnosticDescriptor>();
        var validationArgs = new List<object?[]>();

        // Check if class is partial
        var containingType = methodSymbol.ContainingType;
        var typeDeclaration = containingType.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<TypeDeclarationSyntax>()
            .FirstOrDefault();
        
        if (typeDeclaration is not null && !typeDeclaration.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
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

        // Method must be parameterless
        if (methodSymbol.Parameters.Length > 0)
        {
            validationErrors.Add(MethodHasParametersDescriptor);
            validationArgs.Add(new object?[] { methodSymbol.Name });
        }

        // Check return type
        if (!IsParserReturnType(methodSymbol.ReturnType))
        {
            validationErrors.Add(InvalidReturnTypeDescriptor);
            validationArgs.Add(new object?[] { methodSymbol.Name });
        }

        // Check for [IncludeFiles] attribute
        var includeFilesAttrSymbol = compilation.GetTypeByMetadataName("Parlot.SourceGenerator.IncludeFilesAttribute");
        var additionalFiles = Array.Empty<string>();
        if (includeFilesAttrSymbol is not null)
        {
            var includeFilesAttr = methodSymbol.GetAttributes()
                .FirstOrDefault(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, includeFilesAttrSymbol));
            if (includeFilesAttr is not null && includeFilesAttr.ConstructorArguments.Length > 0)
            {
                var arg = includeFilesAttr.ConstructorArguments[0];
                if (arg.Kind == TypedConstantKind.Array)
                {
                    additionalFiles = arg.Values
                        .Where(v => v.Value is string)
                        .Select(v => (string)v.Value!)
                        .ToArray();
                }
            }
        }

        return new MethodToGenerate(
            methodSymbol, 
            attrLocation, 
            additionalFiles, 
            validationErrors.Count > 0 ? validationErrors.ToArray() : null,
            validationErrors.Count > 0 ? validationArgs.ToArray() : null);
    }

    private readonly record struct MethodToGenerate(
        IMethodSymbol Method, 
        Location? AttributeLocation, 
        string[] AdditionalFiles,
        DiagnosticDescriptor[]? ValidationErrors,
        object?[][]? ValidationArgs);
    private readonly record struct InvocationInfo(string TargetMethodKey, string InterceptsLocationAttribute, Location Location);

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
        MethodToGenerate methodInfo,
        List<InvocationInfo> invocations)
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
        
        // ==== NEW APPROACH: Use LambdaRewriter to transform lambdas into stubs ====
        // The rewriter replaces each lambda/method group with a stub that contains a unique pointer.
        // When the rewritten code executes, LambdaRegistry can extract the pointer and map it
        // back to the original source code.
        
        var rewriter = new LambdaRewriter(semanticModel);
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

        // Create a minimal compilation with the REWRITTEN syntax tree
        var minimalSyntaxTrees = new List<SyntaxTree> { rewrittenTree };
        
        // Add additional files specified by [IncludeFiles] attribute
        if (methodInfo.AdditionalFiles.Length > 0)
        {
            var baseDir = Path.GetDirectoryName(originalSyntaxTree.FilePath) ?? string.Empty;
            foreach (var relativePath in methodInfo.AdditionalFiles)
            {
                var fullPath = Path.GetFullPath(Path.Combine(baseDir, relativePath));
                
                // Find the syntax tree in the host compilation
                var additionalTree = hostCompilation.SyntaxTrees
                    .FirstOrDefault(t => string.Equals(t.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
                
                if (additionalTree is not null)
                {
                    minimalSyntaxTrees.Add(additionalTree);
                }
                else
                {
                    // Try to find by filename only if full path doesn't match
                    var fileName = Path.GetFileName(relativePath);
                    additionalTree = hostCompilation.SyntaxTrees
                        .FirstOrDefault(t => string.Equals(Path.GetFileName(t.FilePath), fileName, StringComparison.OrdinalIgnoreCase));
                    
                    if (additionalTree is not null)
                    {
                        minimalSyntaxTrees.Add(additionalTree);
                    }
                    else
                    {
                        context.ReportDiagnostic(Diagnostic.Create(
                            IncludeFileNotFoundDescriptor,
                            methodSymbol.Locations.FirstOrDefault(), relativePath, methodSymbol.Name));
                    }
                }
            }
        }

        // Add a synthetic file with global usings that Parlot and typical projects provide.
        var globalUsings = CSharpSyntaxTree.ParseText(
            """
            // Parlot global usings for source generator dynamic compilation
            global using System;
            global using System.Collections.Generic;
            global using System.Linq;
            global using System.Threading.Tasks;
            global using Parlot;
            global using Parlot.Fluent;
            global using static Parlot.Fluent.Parsers;
            """,
            parseOptions,
            path: "ParlotGlobalUsings.g.cs");
        minimalSyntaxTrees.Add(globalUsings);
        
        var tempCompilation = hostCompilation
            .RemoveAllSyntaxTrees()
            .AddSyntaxTrees(minimalSyntaxTrees)
            .WithAssemblyName(hostCompilation.AssemblyName + ".ParlotGenTemp")
            .WithOptions(hostCompilation.Options.WithOutputKind(OutputKind.DynamicallyLinkedLibrary));

        using var peStream = new System.IO.MemoryStream();
        var emitResult = tempCompilation.Emit(peStream);

        if (!emitResult.Success)
        {
            // Output a diagnostic about emit failure with the error messages
            var errorMessages = string.Join("; ", emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Take(5)
                .Select(d => d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)));
            
            context.ReportDiagnostic(Diagnostic.Create(
                EmitFailedDescriptor,
                methodSymbol.Locations.FirstOrDefault(), methodSymbol.Name, errorMessages));
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
                    // Try to find the actual assembly in bin folder
                    // Path like: .../obj/Debug/net10.0/ref/Samples.dll -> .../bin/Debug/net10.0/Samples.dll
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
                            methodSymbol.Locations.FirstOrDefault(), refAssemblyName, ex.Message));
                    }
                }
                else
                {
                    var errors = string.Join("; ", refEmitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(3).Select(d => d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)));
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor("PARLOT097", "Emit failed for ref", "Failed to emit '{0}': {1}", "Parlot.SourceGenerator", DiagnosticSeverity.Warning, true),
                        methodSymbol.Locations.FirstOrDefault(), refAssemblyName, errors));
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

            var method = type.GetMethod(
                methodSymbol.Name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            if (method is null)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    MethodNotFoundDescriptor,
                    methodSymbol.Locations.FirstOrDefault(), methodSymbol.Name, containingTypeName));
                return;
            }

            // Invoke the parameterless method to get the parser instance
            object? parserInstance;
            try
            {
                parserInstance = method.Invoke(null, null);
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
                        DiagnosticSeverity.Warning,
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
                        DiagnosticSeverity.Warning,
                        isEnabledByDefault: true),
                    methodInfo.AttributeLocation ?? methodSymbol.Locations.FirstOrDefault(),
                    parserInstance.GetType().FullName));
                return;
            }

            var sgContext = new SourceGenerationContext(parseContextName: "context", methodNamePrefix: methodSymbol.Name);
            
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
            var (sourceText, failedLambdas) = GenerateParserWrapperAndCore(methodSymbol, valueType, sourceResult, sgContext, lambdaSourceMap, rewriter.Lambdas, invocations);

            // Report errors for lambdas that couldn't be extracted
            foreach (var failedLambda in failedLambdas)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    LambdaExtractionFailedDescriptor,
                    methodSymbol.Locations.FirstOrDefault(),
                    methodSymbol.Name,
                    failedLambda));
            }

            // Only report success and add source if there were no failed lambdas
            if (failedLambdas.Count > 0)
            {
                return;
            }

            var hintName = $"{methodSymbol.ContainingType.Name}_{methodSymbol.Name}.Parlot.g.cs";
            var dumpDirectory = Environment.GetEnvironmentVariable("PARLOT_DUMP_GEN_DIR");
            if (!string.IsNullOrEmpty(dumpDirectory))
            {
                Directory.CreateDirectory(dumpDirectory);
                File.WriteAllText(Path.Combine(dumpDirectory, hintName), sourceText);
            }
            context.AddSource(hintName, SourceText.From(sourceText, Encoding.UTF8));
            
            // Report success with the number of intercepted call sites
            context.ReportDiagnostic(Diagnostic.Create(
                GenerationSucceededDescriptor,
                methodInfo.AttributeLocation ?? methodSymbol.Locations.FirstOrDefault(),
                methodSymbol.Name,
                invocations.Count));
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyResolve -= resolver;
        }
    }

    private static (string SourceText, List<string> FailedLambdas) GenerateParserWrapperAndCore(
        IMethodSymbol methodSymbol,
        Type valueType,
        SourceResult result,
        SourceGenerationContext sgContext,
        Dictionary<int, string> lambdaSourceMap,
        IReadOnlyDictionary<int, LambdaRewriter.LambdaInfo> lambdaInfoMap,
        List<InvocationInfo> invocations)
    {
        var ns = methodSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : methodSymbol.ContainingNamespace.ToDisplayString();

        var typeName = methodSymbol.ContainingType.Name;
        var methodName = methodSymbol.Name;
        var valueTypeName = TypeNameHelper.GetTypeName(valueType);
        var coreName = methodName + "_Core";
        var wrapperName = "GeneratedParser_" + methodName;

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
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Parlot;");
        sb.AppendLine("using Parlot.Fluent;");
        sb.AppendLine();
        
        // File-local InterceptsLocationAttribute to avoid conflicts with other generators
        sb.AppendLine("namespace System.Runtime.CompilerServices");
        sb.AppendLine("{");
        sb.AppendLine("    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = true)]");
        sb.AppendLine("    file sealed class InterceptsLocationAttribute : global::System.Attribute");
        sb.AppendLine("    {");
        sb.AppendLine("        public InterceptsLocationAttribute(int version, string data) { }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine();
            sb.Append("namespace ").Append(ns).AppendLine();
            sb.AppendLine("{");
        }

        sb.AppendLine($"    partial class {typeName}");
        sb.AppendLine("    {");

        // ==== NEW POINTER-BASED LAMBDA EMISSION ====
        // Each lambda was rewritten to a stub that captures a unique pointer.
        // The LambdaRegistry extracted these pointers during execution and mapped them to IDs.
        // Now we use the pointer to look up the original source code directly.
        
        var registeredLambdas = sgContext.Lambdas.Enumerate().OrderBy(x => x.Id).ToList();
        var failedLambdas = new List<string>();

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
                
                // Generate a method from the original source
                var methodSource = GenerateLambdaMethod(fieldName, invokeMethod, originalSource, isMethodGroup);
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
                var errorParamList = GenerateParameterList(invokeMethod);
                sb.AppendLine($"        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
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

        sb.AppendLine($"        private sealed class {wrapperName} : Parser<{valueTypeName}>");
        sb.AppendLine("        {");
        sb.AppendLine($"            public override bool Parse(ParseContext context, ref ParseResult<{valueTypeName}> result)");
        sb.AppendLine("            {");
        sb.AppendLine($"                return {coreName}(context, ref result);");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine($"        internal static bool {coreName}(ParseContext context, ref ParseResult<{valueTypeName}> result)");
        sb.AppendLine("        {");
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
                    sb.AppendLine($"            result = new ParseResult<{valueTypeName}>({startOffsetExpr}, cursor.Offset, {result.ValueVariable});");
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
            
            sb.AppendLine($"                result = new ParseResult<{valueTypeName}>({startOffsetExpr}, cursor.Offset, {result.ValueVariable});");
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
            sb.AppendLine($"        private static bool {deferredMethodName}(ParseContext context, out {deferredValueTypeName} value)");
            sb.AppendLine("        {");
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
            sb.AppendLine($"        private static bool {helperMethodName}(ParseContext context, out {helperValueTypeName} value)");
            sb.AppendLine("        {");
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

        // Generate a static field to hold the cached parser instance
        var parserFieldName = $"_generated_{methodName}";
        sb.AppendLine($"        private static readonly Parlot.Fluent.Parser<{valueTypeName}> {parserFieldName} = new {wrapperName}();");
        sb.AppendLine();
        
        // Generate interceptor methods for each invocation site
        if (invocations.Count > 0)
        {
            sb.AppendLine("        // Interceptor methods that redirect calls to the source-generated parser");
            for (int i = 0; i < invocations.Count; i++)
            {
                var inv = invocations[i];
                // Note: InterceptsLocationAttribute already includes brackets from GetInterceptsLocationAttributeSyntax()
                sb.AppendLine($"        {inv.InterceptsLocationAttribute}");
                sb.AppendLine($"        internal static Parlot.Fluent.Parser<{valueTypeName}> {methodName}_Interceptor_{i}() => {parserFieldName};");
                sb.AppendLine();
            }
        }
        else
        {
            // No invocations found - emit a comment and keep a public accessor for manual use
            sb.AppendLine("        // No invocations found to intercept. You can access the generated parser via this property.");
            sb.AppendLine($"        public static Parlot.Fluent.Parser<{valueTypeName}> {methodName}_Generated => {parserFieldName};");
        }

        // Generate helper methods if needed (e.g., CreateCharMap for ListOfChars on netstandard)
        // Note: Currently no helper methods are needed since we use HashSet<char> and SearchValues<char>
        // which are both public types.

        sb.AppendLine("    }");

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine("}");
        }

        return (sb.ToString(), failedLambdas);
    }

    /// <summary>
    /// Generates a method from a lambda source or method group.
    /// Transforms "static x => 'w'" into "private static char MethodName(TextSpan x) => 'w';"
    /// Transforms "char.IsLetterOrDigit" into "private static bool MethodName(char x) => char.IsLetterOrDigit(x);"
    /// </summary>
    private static string GenerateLambdaMethod(string methodName, System.Reflection.MethodInfo invokeMethod, string lambdaSource, bool isMethodGroup)
    {
        var sb = new StringBuilder();
        var returnTypeName = TypeNameHelper.GetTypeName(invokeMethod.ReturnType);
        var parameters = invokeMethod.GetParameters();
        
        // Generate parameter list with types
        var paramList = GenerateParameterList(invokeMethod);
        
        if (isMethodGroup)
        {
            // For method groups like "char.IsLetterOrDigit", generate a call
            // private static bool _lambda0(char x) => char.IsLetterOrDigit(x);
            sb.Append("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]\n");
            sb.Append($"        private static {returnTypeName} {methodName}({paramList}) => ");
            sb.Append(lambdaSource);
            sb.Append('(');
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(GetParameterName(i));
            }
            sb.Append(");");
        }
        else
        {
            // For lambdas, extract the body
            // "static x => 'w'" becomes "'w'"
            var source = lambdaSource.TrimStart();
            if (source.StartsWith("static ", StringComparison.Ordinal))
            {
                source = source.Substring(7).TrimStart();
            }
            
            var arrowIndex = source.IndexOf("=>", StringComparison.Ordinal);
            if (arrowIndex >= 0)
            {
                var body = source.Substring(arrowIndex + 2).Trim();
                var paramPart = source.Substring(0, arrowIndex).Trim();
                
                // Check if it's a block lambda (body starts with {)
                var isBlockLambda = body.StartsWith("{", StringComparison.Ordinal);
                
                if (isBlockLambda)
                {
                    // Block lambda - generate a method with body
                    // Need to replace parameter references in the body
                    body = ReplaceParameterNames(body, paramPart, parameters.Length);
                    
                    sb.Append($"        private static {returnTypeName} {methodName}({paramList})\n");
                    sb.Append("        ");
                    sb.Append(body);
                }
                else
                {
                    // Expression lambda - use expression-bodied method
                    body = ReplaceParameterNames(body, paramPart, parameters.Length);
                    
                    sb.Append("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]\n");
                    sb.Append($"        private static {returnTypeName} {methodName}({paramList}) => ");
                    sb.Append(body);
                    if (!body.EndsWith(";", StringComparison.Ordinal))
                    {
                        sb.Append(';');
                    }
                }
            }
            else
            {
                sb.Append("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]\n");
                sb.Append($"        private static {returnTypeName} {methodName}({paramList}) => default!;");
            }
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Generates a parameter list like "TextSpan arg0, char arg1".
    /// </summary>
    private static string GenerateParameterList(System.Reflection.MethodInfo invokeMethod)
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
    /// Replaces parameter names in the lambda body with standard names.
    /// For "x => x.Length" with 1 param, replaces "x" with "arg0".
    /// For tuple deconstruction "(a, b) => ...", adds a deconstruction statement if it's a block.
    /// </summary>
    private static string ReplaceParameterNames(string body, string paramPart, int paramCount)
    {
        if (paramCount == 0) return body;
        
        // Remove parentheses if present
        paramPart = paramPart.Trim();
        var hadParens = paramPart.StartsWith("(", StringComparison.Ordinal) && paramPart.EndsWith(")", StringComparison.Ordinal);
        if (hadParens)
        {
            paramPart = paramPart.Substring(1, paramPart.Length - 2).Trim();
        }
        
        // Check for tuple deconstruction pattern - multiple names but single param
        var commaCount = paramPart.Count(c => c == ',');
        var isTupleDeconstruction = paramCount == 1 && commaCount > 0;
        
        if (isTupleDeconstruction)
        {
            // Tuple deconstruction case: (a, b) => { ... } with single tuple param
            // We need to add a deconstruction line at the start of the body
            var isBlockBody = body.TrimStart().StartsWith("{", StringComparison.Ordinal);
            if (isBlockBody)
            {
                // Find the opening brace and insert deconstruction after it
                var braceIndex = body.IndexOf('{');
                if (braceIndex >= 0)
                {
                    var deconstructLine = $"\n            var ({paramPart}) = arg0;";
                    body = body.Insert(braceIndex + 1, deconstructLine);
                }
            }
            else
            {
                // Expression body with tuple deconstruction - wrap in block
                // e.g., (a, b) => a + b  becomes { var (a, b) = arg0; return a + b; }
                body = $"{{\n            var ({paramPart}) = arg0;\n            return {body};\n        }}";
            }
            return body;
        }
        
        if (paramCount == 1)
        {
            // Single parameter - the whole paramPart is the name
            var originalName = paramPart.Trim();
            if (!string.IsNullOrEmpty(originalName) && originalName != "arg0")
            {
                // Use regex for whole-word replacement to avoid replacing substrings
                body = System.Text.RegularExpressions.Regex.Replace(
                    body, 
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(originalName)}\b", 
                    "arg0");
            }
        }
        else
        {
            // Multiple parameters - split by comma
            var names = paramPart.Split(',').Select(n => n.Trim()).ToArray();
            for (int i = 0; i < names.Length && i < paramCount; i++)
            {
                var originalName = names[i];
                var newName = GetParameterName(i);
                if (!string.IsNullOrEmpty(originalName) && originalName != newName)
                {
                    body = System.Text.RegularExpressions.Regex.Replace(
                        body, 
                        $@"\b{System.Text.RegularExpressions.Regex.Escape(originalName)}\b", 
                        newName);
                }
            }
        }
        
        return body;
    }
}
