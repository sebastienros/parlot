# SQL Parser for Parlot

This directory contains runtime Fluent and dependency-free source-generated SQL parsers based on the
OrchardCore SQL grammar.

## Files

- `SqlAst.cs`: Complete Abstract Syntax Tree (AST) classes for SQL statements
- `SqlParser.cs`: AST-facing APIs and the runtime Fluent grammar
- `SqlParser.parlot.cs`: Build-only grammar for the direct source-generated API
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

## Example usage

```csharp
var sql = "SELECT * FROM users WHERE id > 10";

// Runtime Fluent parser, including detailed ParseError output.
if (SqlParser.TryParse(sql, out var runtimeResult, out var error))
{
    var statement = runtimeResult.Statements[0].UnionStatements[0].Statement.SelectStatement;
}

// Dependency-free direct parser on .NET 8 and later.
if (SqlParser.TryParse(sql, out var generatedResult))
{
    var statement = generatedResult.Statements[0].UnionStatements[0].Statement.SelectStatement;
}
```
