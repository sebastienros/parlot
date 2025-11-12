# Copilot Instructions for Parlot

## Project Overview

Parlot is a fast, lightweight and simple to use .NET parser combinator library. It provides a fluent API based on parser combinators that offer readable grammar definitions.

## Repository Structure

- `src/Parlot/` - Core library source code
- `src/Samples/` - Example parsers (Calculator, JSON, SQL)
- `test/Parlot.Tests/` - Unit tests
- `test/Parlot.Benchmarks/` - Performance benchmarks
- `docs/` - Documentation files

## Target Frameworks and Build Strategy

### Primary Development Framework
- **Use .NET 9.0 (`net9.0`) as the primary target for development, testing, and validation**
- All new features should be developed and tested against `net9.0` first
- Performance optimizations and benchmarks should target `net9.0`

### Multi-Target Framework Support
The library supports multiple target frameworks:
- `net472` - .NET Framework 4.7.2
- `netstandard2.0` - .NET Standard 2.0 
- `net8.0` - .NET 8.0
- `net9.0` - .NET 9.0
- `net10.0` - .NET 10.0 (primary development target)

**Important**: Only build and test other target frameworks once functionality is proven to work correctly on `net9.0`. This approach improves development velocity and performance.

## Build and Test Commands

### Preferred Commands (Performance Optimized)
```bash
# Build targeting net9.0 only
dotnet build -f net9.0

# Run tests targeting net9.0 only  
dotnet test -f net9.0

# Run benchmarks (net9.0 only)
dotnet run -p test/Parlot.Benchmarks -f net9.0
```

### Full Multi-Target Build (Use Only When Required)
```bash
# Build all target frameworks
dotnet build

# Test all target frameworks
dotnet test
```

## Code Style and Architecture

### Parser Combinator Patterns
- Use the fluent API with static imports: `using static Parlot.Fluent.Parsers;`
- Grammar definitions should be readable and self-documenting
- Prefer composition over inheritance for parser combinators
- Use deferred parsers for recursive grammars

### Performance Considerations
- Parlot prioritizes performance - always consider allocation patterns
- Use `TextSpan` and `ReadOnlySpan<char>` where possible
- Avoid unnecessary string allocations
- Consider compilation for frequently used parsers

### Testing Strategy
- Write comprehensive unit tests for new parser combinators
- Include both positive and negative test cases
- Test error handling and edge cases
- Add benchmarks for performance-critical changes

## Common Development Patterns

### Creating New Parsers
1. Define the grammar using fluent API
2. Write unit tests targeting `net9.0`
3. Add benchmarks if performance is critical
4. Add documentation with examples

### Example Parser Structure
```csharp
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static class MyGrammar
{
    public static readonly Parser<MyResult> MyParser = 
        Terms.Text("keyword")
        .And(Terms.Identifier())
        .And(Terms.Char(';'))
        .Then(result => new MyResult(result.Item2));
}
```

### `And()` builds flat tuples

The `And()` parser combinator builds flat tuples when combining multiple parsers. For example, combining three parsers using `And()` will result in a tuple of three elements.

```c#
var parser = Terms.Char('a').And(Terms.Char('b')).And(Terms.Char('c'));
// The result type is (char, char, char)

parser.Then(result =>
{
    var (first, second, third) = result; // result is a flat tuple (char, char, char)
    // or result.Item1, result.Item2, result.Item3
    return ...;
});
```

### Keyworkds should be skipped
When defining keywords using the `Terms.Text()` parser, these keywords can be skipped in the resulting AST.
`SkipAnd` and `AndSkip` combinators can be used to ignore the keywords while still parsing the surrounding expressions.

```c#
var ifKeyword = Terms.Text("if");
var expressionParser = ...;
var thenKeyword = Terms.Text("then");

var condition = ifKeyword.SkipAnd(expressionParser).AndSkip(thenKeyword).And(expressionParser);
// The result type is (Expression, Expression), skipping the "if" and "then" keywords
```

### Terms and Literals differences

`Terms` parsers are designed to skip whitespace and comments automatically, making them suitable for parsing programming languages or structured text where such elements are common. In contrast, `Literals` parsers do not skip whitespace or comments, providing more control over the parsing process when needed.
It is not necessary to wrap `Terms` parsers with `SkipWhitespace()` as they already handle whitespace and comments internally.

### `Optional()` usage

Use the `Optional()` combinator to make a parser optional. If will always return an instance of `Option<T>` regardless of whether the parser matches or not. Use `HasValue` to check if the parser was successful. Or use `OrSome()` to provide a default value when the parser does not match.

```c#
// Parse an integer or return -1 if not present
var optionalParser = Terms.Integer().Optional().Then(x => x.HasValue ? x.Value : -1);
```

## File Organization

### Core Library (`src/Parlot/`)
- `Character.*.cs` - Character classification and utilities
- `Scanner.cs` - Low-level text scanning
- `Cursor.cs` - Position tracking
- `Fluent/` - High-level fluent API
- `Compilation/` - Parser compilation for performance

### Tests Structure
- Mirror the source structure in test files
- Use descriptive test method names
- Group related tests in test classes

## Documentation Standards

- Include XML documentation for public APIs
- Provide usage examples in documentation
- Update `docs/` folder for significant new features
- Use clear, concise grammar definitions in examples

## Performance Guidelines

### Optimization Priorities
1. Minimize allocations in hot paths
2. Leverage `ReadOnlySpan<char>` for zero-allocation parsing
3. Profile before optimizing - measure actual impact

## Git and Pull Request Guidelines

### Branch Naming
- `feature/description` - New features
- `fix/description` - Bug fixes
- `perf/description` - Performance improvements

### Commit Messages
- Use descriptive commit messages
- Reference issues when applicable
- Separate logical changes into separate commits

### Pull Request Requirements
- Ensure all tests pass on `net9.0`
- Include benchmarks for performance-sensitive changes
- Update documentation for new features
- Validate multi-target compilation before merging

## Special Considerations

### Unsafe Code
- The library uses unsafe code for performance optimizations
- Ensure proper bounds checking and safety measures
- Document any unsafe operations clearly

### Nullable Reference Types
- The project has nullable reference types enabled
- Properly annotate nullable and non-nullable references
- Handle null cases appropriately in public APIs

### Assembly Signing
- The main library is strong-named and signed
- Test projects are not signed
- Maintain key file security

## Resources

- [Parser Combinators Documentation](docs/parsers.md)
- [Writing Parsers Guide](docs/writing.md)
- [Sample Implementations](src/Samples/)
- [Benchmark Results](test/Parlot.Benchmarks/)
