# Source Generation Guide

Parlot supports compile-time source generation using C# interceptors, providing ~20% faster parsing and faster startup compared to runtime-compiled parsers.

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

1. The source generator executes your method at compile time to build the parser graph.
2. It traverses the graph and generates optimized C# code for each parser.
3. C# interceptors replace calls to your method with the generated implementation.
4. At runtime, no graph construction or compilation occurs—just the generated code runs.

## Requirements

- **Static methods**: The annotated method must be `static`.
- **Parameterless**: Methods cannot have parameters (create separate methods for variants).
- **Return type**: Must return `Parlot.Fluent.Parser<T>`.
- **Partial class**: Recommended but not required.

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
[IncludeFiles("../Shared/**/*.cs")]       // Relative paths supported
```

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

To inspect the generated source files, add to your project:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>obj\$(Configuration)\$(TargetFramework)</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Generated files will appear in the specified output path.

## Troubleshooting

### Parser method has parameters

```
error: [GenerateParser] methods must be parameterless
```

Create separate methods for each variant:

```csharp
// ❌ Wrong
[GenerateParser]
public static Parser<string> TextParser(string text) => Terms.Text(text);

// ✅ Correct
[GenerateParser]
public static Parser<string> HelloParser() => Terms.Text("hello");

[GenerateParser]
public static Parser<string> WorldParser() => Terms.Text("world");
```

### Missing types in generated code

If the generated code references types the generator cannot find, use `[IncludeFiles]`:

```csharp
[GenerateParser]
[IncludeFiles("MyAstTypes.cs")]
public static Parser<MyExpression> CreateParser() => ...;
```

### Missing namespaces

Use `[IncludeUsings]` to add required namespaces:

```csharp
[GenerateParser]
[IncludeUsings("System.Collections.Immutable")]
public static Parser<ImmutableList<Token>> CreateParser() => ...;
```

### Custom parser not generating code

Ensure your custom parser implements `ISourceable`. Parsers without this interface fall back to runtime execution.

## Performance Comparison

Source-generated parsers provide:

- **~20% faster parsing** vs. runtime-compiled parsers
- **Faster startup** (no runtime graph building or JIT compilation)
- **AOT compatibility** (deterministic code at compile time)
- **Reduced allocations** during parser construction

See benchmarks in the main [README](../README.md#performance).
