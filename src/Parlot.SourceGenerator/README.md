To debug the generated code, add this to a project:

```xml
  <PropertyGroup>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
    <CompilerGeneratedFilesOutputPath>obj\$(Configuration)\$(TargetFramework)</CompilerGeneratedFilesOutputPath>
  </PropertyGroup>
```

## GenerateParser attribute

- Apply to static methods returning `Parlot.Fluent.Parser<T>`.
- Optional `factoryMethodName` exposes the generated parser as a static property with that name; when omitted, `MethodName_Parser` is used.
- You can pass arguments that map to the method parameters: `GenerateParser("ParseFoo", arg1, arg2, ...)`.
- Multiple attributes are allowed on the same method; **names must be unique**.

Example:

```csharp
[GenerateParser("ParseFoo", "foo")]
[GenerateParser("ParseBar", "bar")]
public static Parser<string> Keyword(string kw) => Terms.Text(kw);
```

