using System;
using System.Collections.Generic;
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
/// Incremental source generator for Parlot grammars.
///
/// It looks for static methods annotated with <see cref="GenerateParserAttribute"/> that:
///   - are static
///   - return Parlot.Fluent.Parser&lt;T&gt;
/// It then:
///   - builds a temporary compilation including those methods,
///   - executes the descriptor methods to obtain Parser&lt;T&gt; instances,
///   - invokes ISourceable.GenerateSource on those instances,
///   - and emits optimized parse methods plus small wrapper Parser&lt;T&gt; types.
/// </summary>
[Generator]
public sealed class ParserSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Find candidate methods syntactically (methods with attributes).
        var methodDeclarations = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => IsCandidateMethod(node),
            static (syntaxContext, _) => GetMethodToGenerate(syntaxContext))
            .Where(static m => m is not null)!;

        // 2. Combine the collected methods with the current Compilation.
        var compilationAndMethods = context.CompilationProvider.Combine(methodDeclarations.Collect());

        // 3. Register for source output.
        context.RegisterSourceOutput(compilationAndMethods, static (spc, source) =>
        {
            var (compilation, methods) = source;

            if (methods.IsDefaultOrEmpty)
            {
                return;
            }

            foreach (var m in methods)
            {
                if (m is null)
                {
                    continue;
                }

                GenerateForMethod(spc, compilation, m.Value);
            }
        });
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

        AttributeData? attrData = null;
        foreach (var attr in methodSymbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, generateParserAttrSymbol))
            {
                attrData = attr;
                break;
            }
        }

        if (attrData is null)
        {
            return null;
        }

        if (!methodSymbol.IsStatic)
        {
            return null;
        }

        if (!IsParserReturnType(methodSymbol.ReturnType))
        {
            return null;
        }

        string? factoryMethodName = null;
        if (attrData.ConstructorArguments.Length == 1)
        {
            var arg = attrData.ConstructorArguments[0];
            if (arg.Value is string s && !string.IsNullOrEmpty(s))
            {
                factoryMethodName = s;
            }
        }

        return new MethodToGenerate(methodSymbol, factoryMethodName);
    }

    private readonly record struct MethodToGenerate(IMethodSymbol Method, string? FactoryMethodName);

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
        MethodToGenerate methodInfo)
    {
        var methodSymbol = methodInfo.Method;
        var factoryMethodName = methodInfo.FactoryMethodName;

        // Create a temporary compilation that we can emit and execute.
        var tempCompilation = hostCompilation.WithAssemblyName(hostCompilation.AssemblyName + ".ParlotGenTemp");

        using var peStream = new System.IO.MemoryStream();
        var emitResult = tempCompilation.Emit(peStream);

        if (!emitResult.Success)
        {
            // Propagate errors as diagnostics so the user sees them.
            foreach (var diagnostic in emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error))
            {
                context.ReportDiagnostic(diagnostic);
            }
            return;
        }

        peStream.Position = 0;
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
            return;
        }

        var method = type.GetMethod(
            methodSymbol.Name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        if (method is null)
        {
            return;
        }

        // Invoke the method to get the Parser<T> instance
        var parserInstance = method.Invoke(null, Array.Empty<object?>());
        if (parserInstance is null)
        {
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
        if (parserInstance is not ISourceable sourceable)
        {
            return;
        }

        var sgContext = new SourceGenerationContext(parseContextName: "context");
        var sourceResult = sourceable.GenerateSource(sgContext);

        // Generate C# code for this particular method
        var sourceText = GenerateParserWrapperAndCore(methodSymbol, valueType, factoryMethodName, sourceResult, sgContext);

        var hintName = $"{methodSymbol.ContainingType.Name}_{methodSymbol.Name}.Parlot.g.cs";
        var dumpDirectory = Environment.GetEnvironmentVariable("PARLOT_DUMP_GEN_DIR");
        if (!string.IsNullOrEmpty(dumpDirectory))
        {
            Directory.CreateDirectory(dumpDirectory);
            File.WriteAllText(Path.Combine(dumpDirectory, hintName), sourceText);
        }
        context.AddSource(hintName, SourceText.From(sourceText, Encoding.UTF8));
    }

    private static string GenerateParserWrapperAndCore(
        IMethodSymbol methodSymbol,
        Type valueType,
        string? factoryMethodName,
        SourceResult result,
        SourceGenerationContext sgContext)
    {
        var ns = methodSymbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : methodSymbol.ContainingNamespace.ToDisplayString();

        var typeName = methodSymbol.ContainingType.Name;
        var methodName = methodSymbol.Name;
        var valueTypeName = TypeNameHelper.GetTypeName(valueType);
        var coreName = methodName + "Core";
        var wrapperName = "GeneratedParser_" + methodName;

        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using System;");
        sb.AppendLine("using Parlot;");
        sb.AppendLine("using Parlot.Fluent;");

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine();
            sb.Append("namespace ").Append(ns).AppendLine();
            sb.AppendLine("{");
        }

        sb.AppendLine($"    partial class {typeName}");
        sb.AppendLine("    {");

        foreach (var (id, del) in sgContext.Lambdas.Enumerate())
        {
            var fieldName = LambdaRegistry.GetFieldName(id);
            var delegateTypeName = TypeNameHelper.GetTypeName(del.GetType());
            var factoryName = $"CreateLambda{id}";

            sb.AppendLine($"        private static readonly {delegateTypeName} {fieldName} = {factoryName}();");

            var method = del.Method;
            var declaringType = method.DeclaringType ?? throw new InvalidOperationException("Delegate has no declaring type.");
            var methodNameLiteral = SymbolDisplay.FormatPrimitive(method.Name, quoteStrings: true, useHexadecimalNumbers: false);
            var bindingFlags = method.IsStatic
                ? "(global::System.Reflection.BindingFlags.Static | global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic)"
                : "(global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic)";
            var typeLiteral = SymbolDisplay.FormatPrimitive(declaringType.FullName ?? declaringType.Name, quoteStrings: true, useHexadecimalNumbers: false);

            sb.AppendLine();
            sb.AppendLine($"        private static {delegateTypeName} {factoryName}()");
            sb.AppendLine("        {");
            if (del.Target != null)
            {
                sb.AppendLine($"            var type = global::System.Reflection.Assembly.GetExecutingAssembly().GetType({typeLiteral}, throwOnError: true)!;");
                sb.AppendLine($"            var method = type.GetMethod({methodNameLiteral}, {bindingFlags});");
                sb.AppendLine("            var ctor = type.GetConstructor(global::System.Reflection.BindingFlags.Instance | global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.NonPublic, binder: null, types: global::System.Type.EmptyTypes, modifiers: null);");
                sb.AppendLine("            var target = ctor != null ? ctor.Invoke(null) : global::System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);");
                sb.AppendLine($"            return ({delegateTypeName})global::System.Delegate.CreateDelegate(typeof({delegateTypeName}), target!, method!);");
            }
            else
            {
                sb.AppendLine($"            var type = global::System.Reflection.Assembly.GetExecutingAssembly().GetType({typeLiteral}, throwOnError: true)!;");
                sb.AppendLine($"            var method = type.GetMethod({methodNameLiteral}, {bindingFlags});");
                sb.AppendLine($"            return ({delegateTypeName})global::System.Delegate.CreateDelegate(typeof({delegateTypeName}), method!);");
            }
            sb.AppendLine("        }");
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
        sb.AppendLine("                // TODO: wire correct span tracking into ISourceable implementation.");
        sb.AppendLine("                var start = context.Scanner.Cursor.Offset;");
        sb.AppendLine("                var end   = start;");
        sb.AppendLine("                result = new ParseResult<" + valueTypeName + ">(start, end, " + result.ValueVariable + ");");
        sb.AppendLine("                return true;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            return false;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Returns a parser that uses the source-generated implementation.");
        sb.AppendLine("        /// Call this from user code instead of the descriptor method if desired.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine($"        public static Parlot.Fluent.Parser<{valueTypeName}> {methodName}_Generated()");
        sb.AppendLine("        {");
        sb.AppendLine($"            return new {wrapperName}();");
        sb.AppendLine("        }");

        // Optional public factory, if the attribute specified one.
        if (!string.IsNullOrEmpty(factoryMethodName))
        {
            if (factoryMethodName!.StartsWith("Parse", StringComparison.Ordinal))
            {
                sb.AppendLine();
                sb.AppendLine("        /// <summary>");
                sb.AppendLine("        /// Convenience parser entry point generated from the descriptor's GenerateParser attribute.");
                sb.AppendLine("        /// </summary>");
                sb.AppendLine($"        public static {valueTypeName} {factoryMethodName}(string text)");
                sb.AppendLine("        {");
                sb.AppendLine($"            var parser = new {wrapperName}();");
                sb.AppendLine("            var context = new ParseContext(new Scanner(text), disableLoopDetection: true);");
                sb.AppendLine($"            var parseResult = new ParseResult<{valueTypeName}>();");
                sb.AppendLine("            if (parser.Parse(context, ref parseResult))");
                sb.AppendLine("            {");
                sb.AppendLine("                return parseResult.Value;");
                sb.AppendLine("            }");
                sb.AppendLine("            throw new InvalidOperationException(\"Parsing failed.\");");
                sb.AppendLine("        }");
            }
            else
            {
                sb.AppendLine();
                sb.AppendLine("        /// <summary>");
                sb.AppendLine("        /// Public factory method generated from the descriptor's GenerateParser attribute.");
                sb.AppendLine("        /// </summary>");
                sb.AppendLine($"        public static Parlot.Fluent.Parser<{valueTypeName}> {factoryMethodName}()");
                sb.AppendLine("        {");
                sb.AppendLine($"            return new {wrapperName}();");
                sb.AppendLine("        }");
            }
        }

        sb.AppendLine("    }");

        if (!string.IsNullOrEmpty(ns))
        {
            sb.AppendLine("}");
        }

        return sb.ToString();
    }
}
