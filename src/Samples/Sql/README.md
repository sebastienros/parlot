# SQL Parser for Parlot

This directory contains a SQL parser implementation using Parlot's Fluent API, based on the OrchardCore SQL Grammar.

## Files

- `SqlAst.cs`: Complete Abstract Syntax Tree (AST) classes for SQL statements
- `SqlParser.cs`: Parser implementation using Parlot (Work in Progress)
- `../../test/Parlot.Tests/Sql/SqlParserTests.cs`: Test suite

## Supported SQL Features

Based on the OrchardCore SQL Grammar, the parser supports:

### Statements
- SELECT statements with full feature set
- WITH clauses (Common Table Expressions/CTEs)
- UNION and UNION ALL

### SELECT Clauses
- SELECT DISTINCT / ALL
- Column selection (*, specific columns, functions)
- FROM clause with tables and subqueries
- WHERE clause with complex expressions
- JOIN (INNER, LEFT, RIGHT) with conditions
- GROUP BY
- HAVING  
- ORDER BY (ASC/DESC)
- LIMIT
- OFFSET

### Expressions
- Binary operators (arithmetic, comparison, logical, bitwise)
- Unary operators (NOT, +, -, ~)
- BETWEEN
- IN
- LIKE / NOT LIKE
- Function calls with OVER (window functions)
- Parameters (@param)
- Literals (numbers, strings, booleans)
- Identifiers (simple and dotted notation)

## Current Status

- ✅ AST classes are complete and ready to use
- ⚠️  Parser implementation is structurally complete but has compilation errors
- ✅ Test cases are defined

## Next Steps

To complete the parser:
1. Fix tuple unwrapping in complex parser combinators
2. Resolve type casting issues for generic collections
3. Test and validate with the defined test cases
4. Add additional test coverage

## Example Usage (when complete)

```csharp
var result = SqlParser.Parse("SELECT * FROM users WHERE id > 10");
if (result != null)
{
    // Work with the AST
    var statement = result.Statements[0].UnionStatements[0].Statement.SelectStatement;
    // ...
}
```
