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

