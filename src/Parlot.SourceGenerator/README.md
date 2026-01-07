To debug the generated code, add this to a project:

```xml
  <PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>obj\$(Configuration)\$(TargetFramework)</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>
```

## GenerateParser attribute

- Apply `[GenerateParser]` to static, **parameterless** methods returning `Parlot.Fluent.Parser<T>`.
- Uses C# interceptors to replace calls to the annotated method with generated, optimized code at compile time.
- Each method can only have one `[GenerateParser]` attribute.
- If you need multiple parser variants (e.g., different keywords), create separate methods.

Example:

```csharp
using Parlot.SourceGenerator;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static partial class MyGrammar
{
    // Simple parser
    [GenerateParser]
    public static Parser<string> HelloParser() => Terms.Text("hello");

    // For variants, create separate methods instead of parameterized ones
    [GenerateParser]
    public static Parser<string> FooParser() => Terms.Text("foo");

    [GenerateParser]
    public static Parser<string> BarParser() => Terms.Text("bar");
}

// Usage - calls are intercepted and replaced with generated code
var hello = MyGrammar.HelloParser();
var foo = MyGrammar.FooParser();
```

## Helper Attributes

### IncludeFiles

Include additional source files for types your parser depends on. Paths are relative to the source file containing the `[GenerateParser]` method.

```csharp
[GenerateParser]
[IncludeFiles("Ast.cs", "Tokens.cs")]
public static Parser<Expression> CreateParser() => ...;
```

Glob patterns are supported:
- `*` – matches any characters except path separator
- `**` – matches recursively (any depth)
- `?` – matches single character

```csharp
[IncludeFiles("Models/*.cs", "../Shared/**/*.cs")]
```

### IncludeUsings

Add using directives to the generated code:

```csharp
[GenerateParser]
[IncludeUsings("System.Collections.Generic", "MyProject.Models")]
public static Parser<Expression> CreateParser() => ...;
```

### IncludeGenerators

Run other source generators (e.g., PolySharp) before parser generation:

```csharp
[GenerateParser]
[IncludeGenerators("PolySharp")]
public static Parser<Expression> CreateParser() => ...;
```

### Class-level Attributes

Helper attributes can be applied at class level to affect all parsers:

```csharp
[IncludeFiles("Ast.cs")]
[IncludeUsings("MyProject.Models")]
public static partial class MyParsers
{
    [GenerateParser]
    public static Parser<Expression> ExprParser() => ...;

    [GenerateParser]
    [IncludeFiles("Extra.cs")]  // Combined with class-level
    public static Parser<Statement> StmtParser() => ...;
}
```

## Custom Parsers with ISourceable

To make custom parsers work with source generation, implement `ISourceable`:

```csharp
public class MyCustomParser : Parser<string>, ISourceable
{
    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        var res = context.CreateResult(typeof(string));
        
        res.Body.Add($"// Custom parsing logic");
        res.Body.Add($"{res.SuccessVariable} = true;");
        res.Body.Add($"{res.ValueVariable} = \"result\";");
        
        return res;
    }
}
```

For comprehensive documentation, see [Source Generation Guide](../../docs/source-generation.md).

