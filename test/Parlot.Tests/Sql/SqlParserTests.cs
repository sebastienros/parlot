using Xunit;

namespace Parlot.Tests.Sql;

public class SqlParserTests
{
    [Theory]
    [InlineData("SELECT * FROM users")]
    [InlineData("SELECT id, name FROM users")]
    [InlineData("SELECT id, name FROM users WHERE id = 1")]
    [InlineData("SELECT * FROM users WHERE name = 'John'")]
    [InlineData("SELECT * FROM users ORDER BY id ASC")]
    [InlineData("SELECT * FROM users LIMIT 10")]
    [InlineData("SELECT * FROM users OFFSET 5")]
    public void ShouldParseBasicSelectStatements(string sql)
    {
        var result = SqlParser.Parse(sql);
        Assert.NotNull(result);
        Assert.Single(result.Statements);
    }

    [Theory]
    [InlineData("SELECT COUNT(*) FROM users")]
    [InlineData("SELECT COUNT(id) FROM users")]
    [InlineData("SELECT SUM(amount), AVG(price) FROM orders")]
    public void ShouldParseFunctionCalls(string sql)
    {
        var result = SqlParser.Parse(sql);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("SELECT * FROM users WHERE id > 10 AND name LIKE 'A%'")]
    [InlineData("SELECT * FROM users WHERE id BETWEEN 1 AND 100")]
    [InlineData("SELECT * FROM users WHERE id IN (1, 2, 3)")]
    [InlineData("SELECT * FROM users WHERE NOT active")]
    public void ShouldParseComplexWhereConditions(string sql)
    {
        var result = SqlParser.Parse(sql);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("SELECT u.id, u.name FROM users u")]
    [InlineData("SELECT u.id, o.amount FROM users u JOIN orders o ON u.id = o.user_id")]
    [InlineData("SELECT * FROM users u LEFT JOIN orders o ON u.id = o.user_id")]
    [InlineData("SELECT * FROM users u INNER JOIN orders o ON u.id = o.user_id")]
    public void ShouldParseJoins(string sql)
    {
        var result = SqlParser.Parse(sql);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("SELECT category, COUNT(*) FROM products GROUP BY category")]
    [InlineData("SELECT category, COUNT(*) FROM products GROUP BY category HAVING COUNT(*) > 10")]
    public void ShouldParseGroupByAndHaving(string sql)
    {
        var result = SqlParser.Parse(sql);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("SELECT * FROM users UNION SELECT * FROM customers")]
    [InlineData("SELECT * FROM users UNION ALL SELECT * FROM customers")]
    public void ShouldParseUnion(string sql)
    {
        var result = SqlParser.Parse(sql);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("WITH cte AS (SELECT * FROM users) SELECT * FROM cte")]
    [InlineData("WITH cte(id, name) AS (SELECT id, name FROM users) SELECT * FROM cte", Skip = "TBD")]
    public void ShouldParseCommonTableExpressions(string sql)
    {
        var result = SqlParser.Parse(sql);
        Assert.NotNull(result);
    }

    [Fact]
    public void ShouldReturnNullForInvalidSQL()
    {
        var result = SqlParser.Parse("INVALID SQL STATEMENT");
        Assert.Null(result);
    }
}
