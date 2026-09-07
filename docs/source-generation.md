# Source Generation Guide

Parlot supports compile-time source generation using C# interceptors, providing faster parsing and startup than equivalent Fluent parser graphs.

## Quick Start

1. Add the interceptors namespace to your project file:

```xml
<PropertyGroup>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);YourNamespace</InterceptorsNamespaces>
</PropertyGroup>
```

2. Annotate your parser method:

```csharp
using Parlot.SourceGenerator;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static partial class MyGrammar
{
    [GenerateParser]
    public static Parser<string> HelloParser() => Terms.Text("hello");
}
```

3. Use the parser normally—calls are intercepted automatically:

```csharp
var parser = MyGrammar.HelloParser();  // Uses generated code
var result = parser.Parse("hello world");
```

## How It Works

1. The source generator executes your method at compile time inside the compiler host to build the parser graph.
2. It traverses the graph and generates optimized C# code for each parser.
3. C# interceptors replace calls to your method with the generated implementation.
4. At runtime, no parser graph construction occurs—just the generated code runs.

Generated `ZeroOrMany`, `OneOrMany`, and `Separated` parsers use the same collection storage as
runtime parsers: up to four elements are stored inline, with no separate backing array. Larger
results use a growing `List<T>`, and an empty `ZeroOrMany` result uses `Array.Empty<T>()`.
Results remain exposed as `IReadOnlyList<T>`; their concrete type is an implementation detail.
`Parlot.Fluent.HybridList<T>` is public, but hidden from IntelliSense, to support generated code
in consumer assemblies. It is a result builder, not a general-purpose collection API.

> [!WARNING]
> `[GenerateParser]` factory methods are executable build code, not declarative metadata. They run with the
> compiler host's process permissions and can access the build environment. `[IncludeGenerators]` additionally
> loads and executes the selected analyzer assemblies, which is equivalent to trusting executable build
> dependencies. Only build trusted parser factories and generators outside an isolated environment. See the
> [security guidance](security.md#source-generation-and-build-time-code-execution).

## Requirements

- **Static methods**: The annotated method must be `static`.
- **Parameters**: Ordinary by-value arguments can be captured by inline parse-time callbacks. They cannot determine the parser graph during construction.
- **Non-generic**: Generic factories and generic containing types are not supported. Neither are `ref`, `out`, `in`, ref-like, pointer, or function-pointer parameters.
- **Return type**: Must return `Parlot.Fluent.Parser<T>`.
- **Partial class**: The containing class must be `partial`.

## IDE and Design-Time Analysis

Parlot skips parser generation, factory execution, and its generation diagnostics during recognized
IDE/design-time analysis. The original parser factory remains available without generated interceptors.
Other generators, such as PolySharp, still run normally in the IDE; Parlot does not load and execute
them through `[IncludeGenerators]` during these runs.

Command-line builds and explicit builds started inside Visual Studio still generate parsers and
interceptors. Parlot skips generation when `DesignTimeBuild` is `true`, or when
`BuildingInsideVisualStudio` is `true` and `BuildingProject` is explicitly `false`.
Missing property values alone do not disable generation.

The Parlot NuGet package exposes these properties to the compiler through its `buildTransitive` props.
Keep those package assets enabled so the generator can distinguish live analysis from a real build.

## Parameterized Parsers

Factory arguments configure a generated parser instance. The generator emits every branch once; the
actual arguments are bound when the factory is called at runtime.

```csharp
public static partial class MyGrammar
{
    [GenerateParser]
    public static Parser<string> Greeting(bool formal, string prefix)
    {
        return If(
            () => formal,
            Literals.Text("Hello"),
            Literals.Text("Hi"))
            .Then(text => prefix + text);
    }
}

var formal = MyGrammar.Greeting(true, "Greeting: ");
var informal = MyGrammar.Greeting(false, "");

formal.Parse("Hello"); // "Greeting: Hello"
informal.Parse("Hi");  // "Hi"
```

Multiple parameters, overloads, named arguments, optional arguments, and `params` arrays are supported.
Calls evaluate their arguments normally, once and in source order. Parameterless factories still return
a cached singleton; parameterized factories create a small bound parser instance, not a parser graph.
Build that instance once and reuse it.

### Conditional branches and context

`If(condition, thenParser, elseParser)` evaluates the predicate once each time it is parsed and runs only
the selected branch. Failure does not try the other branch. `If(condition, parser)` fails without
consuming input when the condition is false. Both forms also accept `Func<ParseContext, bool>` or
`Func<C, bool>` where `C : ParseContext`:

```csharp
return If(
    (LanguageParseContext context) => options.AllowExtensions && context.InsideFunction,
    extensionExpression,
    standardExpression);
```

Factory arguments belong to the parser instance; the context always comes from the current parse.
Predicates must not consume input. Use `Select(() => index, a, b, c)` for multiple branches, or its
context-aware overloads. All branch parsers are constructed at compile time, including inactive ones.

### Capture restrictions

Factory parameters may be read in inline callbacks passed to `If`, `Select`, `Then`, `ThenElse`, `When`,
`Switch`, and `Else`. Generated callbacks access instance fields directly, without allocating closures
or invoking delegates during parsing. An ordinary, non-intercepted factory call retains its normal
runtime combinator behavior.

Captured arguments retain normal value/reference semantics. A reference-type options object is not
cloned or frozen, so later mutations are visible to predicates. Prefer immutable options for parsers
shared across threads. Value-type arguments are copied when the factory is called.

Parameters cannot be reassigned or passed by reference. Factory-parameter captures must be inline;
storing such a callback in a local variable is not supported. Captured locals and captures of parameters
belonging to a different helper method are also unsupported. Move calculations that depend on arguments
into the callback, or compute them before calling the factory.

The graph must not depend on placeholder argument values. These examples are rejected with `PARLOT021`:

```csharp
// Eager branch selection would omit one branch from the generated parser.
return formal ? Literals.Text("Hello") : Literals.Text("Hi");

// A runtime-configured literal is not a compile-time constant.
return Literals.Text(keyword);

// Even eager normalization reads an unavailable runtime argument.
var normalized = prefix.Trim();
return Literals.Text("Hello").Then(text => normalized + text);
```

Use `If` or `Select` for alternatives and callbacks such as `.Then(text => prefix.Trim() + text)` for
deferred computation. Eager argument validation also belongs before the factory call. Deferred callbacks
are identified without executing their user bodies for source extraction; a callback capturing factory
state cannot be invoked while building the graph. Such execution is rejected even if the factory catches
the resulting exception (`PARLOT022`).

## Attributes Reference

### [GenerateParser]

Marks a method for source generation.

```csharp
[GenerateParser]
public static Parser<Expression> CreateParser() => ...;
```

### [IncludeFiles]

Includes additional source files in the compilation. Paths are resolved relative to the source file containing the `[GenerateParser]` method.

```csharp
[GenerateParser]
[IncludeFiles("Ast.cs", "Tokens.cs")]
public static Parser<Expression> CreateParser() => ...;
```

#### Glob Patterns

| Pattern | Description |
|---------|-------------|
| `*` | Matches any characters except path separator |
| `**` | Matches any characters including path separators (recursive) |
| `?` | Matches any single character except path separator |

Examples:

```csharp
[IncludeFiles("*.cs")]                    // All .cs files in same directory
[IncludeFiles("Models/*.cs")]             // All .cs files in Models subdirectory
[IncludeFiles("**/*.cs")]                 // All .cs files recursively
[IncludeFiles("../Shared/**/*.cs")]       // Parent paths are allowed only within the project root
```

Paths are resolved from the parser source file but must remain inside the MSBuild project root. Absolute
paths, symbolic-link traversal, and patterns not ending in `.cs` are rejected. A parser can include at most
256 files, each no larger than 1 MiB and no larger than 8 MiB in total; glob traversal is capped at 10,000
entries. Rejections are reported as `PARLOT016`-`PARLOT020` diagnostics without resolved filesystem paths.
Use exact files or narrow directory globs rather than project-wide `**/*.cs` patterns.

### [IncludeUsings]

Adds using directives to the generated code.

```csharp
[GenerateParser]
[IncludeUsings("System.Collections.Generic", "MyProject.Models")]
public static Parser<Expression> CreateParser() => ...;
```

### [IncludeGenerators]

Specifies source generator assemblies to run before parser generation. Use when your parser depends on code produced by other generators.

```csharp
[GenerateParser]
[IncludeGenerators("PolySharp")]
public static Parser<Expression> CreateParser() => ...;

// Multiple generators
[IncludeGenerators("PolySharp", "Microsoft.Extensions.Logging.Generators")]
```

Included generators execute in the compiler host. Treat every named generator and its transitive package
dependencies as trusted executable build code.

### Class-Level Attributes

`[IncludeFiles]`, `[IncludeUsings]`, and `[IncludeGenerators]` can be applied at class level to affect all parsers in the class:

```csharp
[IncludeFiles("Ast.cs")]
[IncludeUsings("MyProject.Models")]
[IncludeGenerators("PolySharp")]
public static partial class SqlParsers
{
    [GenerateParser]
    public static Parser<SelectStatement> SelectParser() => ...;

    [GenerateParser]
    public static Parser<InsertStatement> InsertParser() => ...;

    [GenerateParser]
    [IncludeFiles("DeleteAst.cs")]  // Combined with class-level includes
    public static Parser<DeleteStatement> DeleteParser() => ...;
}
```

## Custom Parsers with ISourceable

Built-in Parlot parsers implement `ISourceable` for source generation. To make custom parsers compatible, implement the interface:

```csharp
using Parlot.SourceGeneration;

public class KeywordParser : Parser<string>, ISeekable, ISourceable
{
    private readonly string _keyword;

    public KeywordParser(string keyword) => _keyword = keyword;

    // ISeekable implementation
    public bool CanSeek => true;
    public char[] ExpectedChars => new[] { _keyword[0] };
    public bool SkipWhitespace => true;

    // Runtime parsing
    public override bool Parse(ParseContext context, ref ParseResult<string> result)
    {
        context.SkipWhiteSpace();
        var start = context.Scanner.Cursor.Position;
        
        if (context.Scanner.ReadText(_keyword))
        {
            result.Set(start.Offset, context.Scanner.Cursor.Offset, _keyword);
            return true;
        }
        return false;
    }

    // Source generation
    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        var res = context.CreateResult(typeof(string));
        var ctx = context.ParseContextName;
        var cursor = context.CursorName;

        res.Body.Add($"{ctx}.SkipWhiteSpace();");
        res.Body.Add($"var __start = {cursor}.Position;");
        res.Body.Add($"if ({ctx}.Scanner.ReadText(\"{_keyword}\"))");
        res.Body.Add("{");
        res.Body.Add($"    {res.SuccessVariable} = true;");
        res.Body.Add($"    {res.ValueVariable} = \"{_keyword}\";");
        res.Body.Add("}");

        return res;
    }
}
```

### SourceGenerationContext API

| Member | Description |
|--------|-------------|
| `CreateResult(Type)` | Creates a `SourceResult` for the given return type |
| `ParseContextName` | Variable name for the `ParseContext` |
| `CursorName` | Variable name for the cursor |
| `ScannerName` | Variable name for the scanner |

### SourceResult API

| Member | Description |
|--------|-------------|
| `Body` | List of code statements to emit |
| `SuccessVariable` | Variable name to set for success (`true`/`false`) |
| `ValueVariable` | Variable name to assign the parsed value |
| `DeclareSubExpression<T>()` | Declare a helper variable |

## Debugging Generated Code

### Inspecting Generated Files

To inspect the generated source files, add to your project:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>obj\$(Configuration)\$(TargetFramework)</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Generated files will appear in the specified output path.

### Lambda Debugging Support

The source generator emits `#line` directives in the generated code, which enables the debugger to map breakpoints and step-through debugging back to your original lambda expressions.

When you set a breakpoint on a lambda in your parser definition:

```csharp
[GenerateParser]
public static Parser<string> MyParser()
{
    return Terms.Identifier().Then(static x => x.ToString().ToUpper()); // Breakpoint here works!
}
```

The debugger will stop at the original source location, even though the actual execution is in the generated static method.

The generator preserves your original parameter names in the generated code:

```csharp
// Original lambda:
static (a, b) => a + b

// Generated method (with original names preserved):
#line 42 "/path/to/MyParser.cs"
private static decimal _lambda0(decimal a, decimal b) => a + b;
#line default
```

For multi-line block lambdas, each line gets its own `#line` directive:

```csharp
// Original block lambda:
static x => {
    var upper = x.ToString().ToUpper();
    var result = "Result: " + upper;
    return result;
}

// Generated method (each line mapped):
private static string _lambda0(TextSpan x)
{
#line 10 "/path/to/MyParser.cs"
    var upper = x.ToString().ToUpper();
#line 11 "/path/to/MyParser.cs"
    var result = "Result: " + upper;
#line 12 "/path/to/MyParser.cs"
    return result;
}
#line default
```

This ensures that:
- Breakpoints work correctly on any line of the lambda expression
- Variable names in the debugger match your original code
- Step-through debugging works line-by-line through the lambda body

## Troubleshooting

### Unsupported factory parameters

```
error PARLOT009: Unsupported parser factory signature
```

Use ordinary by-value parameters, not generic factories, by-reference parameters, or ref-like types.
If an ordinary parameter is used eagerly during graph construction, `PARLOT021` explains that it must
instead be read in a supported parse-time callback:

```csharp
// ❌ Wrong
[GenerateParser]
public static Parser<string> TextParser(bool formal) =>
    formal ? Terms.Text("Hello") : Terms.Text("Hi");

// ✅ Correct
[GenerateParser]
public static Parser<string> TextParser(bool formal) =>
    If(() => formal, Terms.Text("Hello"), Terms.Text("Hi"));
```

### Unsupported captures

```
error PARLOT015: Lambda captures variable 'prefix' from the enclosing scope
```

Lambdas may capture their factory's parameters, but not arbitrary locals or another method's parameters.

```csharp
// ❌ Wrong - captures 'prefix' variable
[GenerateParser]
public static Parser<string> MyParser()
{
    var prefix = "hello";  // Captured variable
    return Terms.Identifier().Then(x => prefix + x.ToString());  // Error!
}
```

**Solutions:**

1. **Pass the state as a factory parameter:**

```csharp
[GenerateParser]
public static Parser<string> MyParser(string prefix) =>
    Terms.Identifier().Then(x => prefix + x.ToString());
```

2. **Use a custom `ParseContext` subclass** for per-execution state:

```csharp
public class MyParseContext : ParseContext
{
    public MyParseContext(Scanner scanner) : base(scanner) { }
    public string Prefix { get; set; } = "";
}

[GenerateParser]
public static Parser<string> MyParser()
{
    return Terms.Identifier().Then(
        static (context, x) => ((MyParseContext)context).Prefix + x.ToString());
}

// Usage:
var parser = MyParser();
var context = new MyParseContext(new Scanner("world")) { Prefix = "hello" };
parser.Parse(context, out var result);
```

3. **Use static fields or properties:**

```csharp
private static string _prefix = "hello";

[GenerateParser]
public static Parser<string> MyParser()
{
    return Terms.Identifier().Then(static x => _prefix + x.ToString());
}
```

4. **Use method groups for static methods:**

```csharp
private static string Transform(TextSpan x) => "hello" + x.ToString();

[GenerateParser]
public static Parser<string> MyParser()
{
    return Terms.Identifier().Then(Transform);
}
```


### Custom parser not generating code

Ensure your custom parser implements `ISourceable`. Parsers without this interface fall back to runtime execution.

## Performance Comparison

Source-generated parsers provide:

- **Faster parsing** than equivalent Fluent parser graphs
- **Faster startup** (no runtime parser graph construction)
- **AOT compatibility** (deterministic code at compile time)
- **Reduced allocations** during parser construction

See benchmarks in the main [README](../README.md#performance).
