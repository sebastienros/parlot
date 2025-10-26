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
    [InlineData("WITH cte(id, name) AS (SELECT id, name FROM users) SELECT * FROM cte")]
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

    // Comprehensive structural validation tests

    [Fact]
    public void ParsedSelectShouldHaveCorrectStructure()
    {
        var result = SqlParser.Parse("SELECT * FROM users");
        
        Assert.NotNull(result);
        Assert.Single(result.Statements);
        
        var statement = GetSelectStatement(result);
        Assert.IsType<StarSelector>(statement.SelectorList);
        Assert.NotNull(statement.FromClause);
        Assert.Single(statement.FromClause.TableSources);
        
        var table = Assert.IsType<TableSourceItem>(statement.FromClause.TableSources[0]);
        Assert.Equal("users", table.Identifier.ToString());
        Assert.Null(statement.WhereClause);
    }

    [Fact]
    public void ParsedSelectWithColumnsShouldHaveCorrectColumns()
    {
        var result = SqlParser.Parse("SELECT id, name FROM users");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        var columnList = Assert.IsType<ColumnItemList>(statement.SelectorList);
        Assert.Equal(2, columnList.Columns.Count);
        
        var col1 = Assert.IsType<ColumnSourceIdentifier>(columnList.Columns[0].Source);
        Assert.Equal("id", col1.Identifier.ToString());
        
        var col2 = Assert.IsType<ColumnSourceIdentifier>(columnList.Columns[1].Source);
        Assert.Equal("name", col2.Identifier.ToString());
    }

    [Fact]
    public void ParsedWhereClauseShouldHaveCorrectExpression()
    {
        var result = SqlParser.Parse("SELECT id, name FROM users WHERE id = 1");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.WhereClause);
        var whereExpr = Assert.IsType<BinaryExpression>(statement.WhereClause.Expression);
        Assert.Equal(BinaryOperator.Equal, whereExpr.Operator);
        
        var left = Assert.IsType<IdentifierExpression>(whereExpr.Left);
        Assert.Equal("id", left.Identifier.ToString());
        
        var right = Assert.IsType<LiteralExpression>(whereExpr.Right);
        Assert.Equal(1m, right.Value);
    }

    [Fact]
    public void ParsedStringLiteralShouldHaveCorrectValue()
    {
        var result = SqlParser.Parse("SELECT * FROM users WHERE name = 'John'");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.WhereClause);
        var whereExpr = Assert.IsType<BinaryExpression>(statement.WhereClause.Expression);
        
        var right = Assert.IsType<LiteralExpression>(whereExpr.Right);
        Assert.Equal("John", right.Value);
    }

    [Fact]
    public void ParsedOrderByClauseShouldHaveCorrectDirection()
    {
        var result = SqlParser.Parse("SELECT * FROM users ORDER BY id ASC");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.OrderByClause);
        Assert.Single(statement.OrderByClause.Items);
        
        var orderItem = statement.OrderByClause.Items[0];
        Assert.Equal("id", orderItem.Identifier.ToString());
        Assert.Equal(OrderDirection.Asc, orderItem.Direction);
    }

    [Fact]
    public void ParsedLimitClauseShouldHaveCorrectValue()
    {
        var result = SqlParser.Parse("SELECT * FROM users LIMIT 10");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.LimitClause);
        var limitExpr = Assert.IsType<LiteralExpression>(statement.LimitClause.Expression);
        Assert.Equal(10m, limitExpr.Value);
    }

    [Fact]
    public void ParsedOffsetClauseShouldHaveCorrectValue()
    {
        var result = SqlParser.Parse("SELECT * FROM users OFFSET 5");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.OffsetClause);
        var offsetExpr = Assert.IsType<LiteralExpression>(statement.OffsetClause.Expression);
        Assert.Equal(5m, offsetExpr.Value);
    }

    [Fact]
    public void ParsedFunctionWithStarShouldHaveStarArgument()
    {
        var result = SqlParser.Parse("SELECT COUNT(*) FROM users");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        var columnList = Assert.IsType<ColumnItemList>(statement.SelectorList);
        Assert.Single(columnList.Columns);
        
        var funcSource = Assert.IsType<ColumnSourceFunction>(columnList.Columns[0].Source);
        Assert.Equal("COUNT", funcSource.FunctionCall.Name.ToString());
        Assert.IsType<StarArgument>(funcSource.FunctionCall.Arguments);
    }

    [Fact]
    public void ParsedFunctionWithExpressionShouldHaveExpressionArguments()
    {
        var result = SqlParser.Parse("SELECT COUNT(id) FROM users");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        var columnList = Assert.IsType<ColumnItemList>(statement.SelectorList);
        var funcSource = Assert.IsType<ColumnSourceFunction>(columnList.Columns[0].Source);
        
        Assert.Equal("COUNT", funcSource.FunctionCall.Name.ToString());
        var exprArgs = Assert.IsType<ExpressionListArguments>(funcSource.FunctionCall.Arguments);
        Assert.Single(exprArgs.Expressions);
        
        var arg = Assert.IsType<IdentifierExpression>(exprArgs.Expressions[0]);
        Assert.Equal("id", arg.Identifier.ToString());
    }

    [Fact]
    public void ParsedMultipleFunctionsShouldHaveCorrectFunctionNames()
    {
        var result = SqlParser.Parse("SELECT SUM(amount), AVG(price) FROM orders");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        var columnList = Assert.IsType<ColumnItemList>(statement.SelectorList);
        Assert.Equal(2, columnList.Columns.Count);
        
        var func1 = Assert.IsType<ColumnSourceFunction>(columnList.Columns[0].Source);
        Assert.Equal("SUM", func1.FunctionCall.Name.ToString());
        
        var func2 = Assert.IsType<ColumnSourceFunction>(columnList.Columns[1].Source);
        Assert.Equal("AVG", func2.FunctionCall.Name.ToString());
    }

    [Fact]
    public void ParsedAndExpressionShouldHaveCorrectOperators()
    {
        var result = SqlParser.Parse("SELECT * FROM users WHERE id > 10 AND name LIKE 'A%'");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.WhereClause);
        var andExpr = Assert.IsType<BinaryExpression>(statement.WhereClause.Expression);
        Assert.Equal(BinaryOperator.And, andExpr.Operator);
        
        var leftExpr = Assert.IsType<BinaryExpression>(andExpr.Left);
        Assert.Equal(BinaryOperator.GreaterThan, leftExpr.Operator);
        
        var rightExpr = Assert.IsType<BinaryExpression>(andExpr.Right);
        Assert.Equal(BinaryOperator.Like, rightExpr.Operator);
    }

    [Fact]
    public void ParsedBetweenExpressionShouldHaveCorrectBounds()
    {
        var result = SqlParser.Parse("SELECT * FROM users WHERE id BETWEEN 1 AND 100");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.WhereClause);
        var betweenExpr = Assert.IsType<BetweenExpression>(statement.WhereClause.Expression);
        
        Assert.False(betweenExpr.IsNot);
        
        var exprId = Assert.IsType<IdentifierExpression>(betweenExpr.Expression);
        Assert.Equal("id", exprId.Identifier.ToString());
        
        var lower = Assert.IsType<LiteralExpression>(betweenExpr.Lower);
        Assert.Equal(1m, lower.Value);
        
        var upper = Assert.IsType<LiteralExpression>(betweenExpr.Upper);
        Assert.Equal(100m, upper.Value);
    }

    [Fact]
    public void ParsedInExpressionShouldHaveCorrectValues()
    {
        var result = SqlParser.Parse("SELECT * FROM users WHERE id IN (1, 2, 3)");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.WhereClause);
        var inExpr = Assert.IsType<InExpression>(statement.WhereClause.Expression);
        
        Assert.False(inExpr.IsNot);
        Assert.Equal(3, inExpr.Values.Count);
        
        var val1 = Assert.IsType<LiteralExpression>(inExpr.Values[0]);
        Assert.Equal(1m, val1.Value);
        
        var val2 = Assert.IsType<LiteralExpression>(inExpr.Values[1]);
        Assert.Equal(2m, val2.Value);
        
        var val3 = Assert.IsType<LiteralExpression>(inExpr.Values[2]);
        Assert.Equal(3m, val3.Value);
    }

    [Fact]
    public void ParsedUnaryNotExpressionShouldHaveCorrectOperator()
    {
        var result = SqlParser.Parse("SELECT * FROM users WHERE NOT active");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.WhereClause);
        var unaryExpr = Assert.IsType<UnaryExpression>(statement.WhereClause.Expression);
        Assert.Equal(UnaryOperator.Not, unaryExpr.Operator);
        
        var innerExpr = Assert.IsType<IdentifierExpression>(unaryExpr.Expression);
        Assert.Equal("active", innerExpr.Identifier.ToString());
    }

    [Fact]
    public void ParsedTableAliasShouldBeCorrect()
    {
        var result = SqlParser.Parse("SELECT u.id, u.name FROM users u");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.FromClause);
        var table = Assert.IsType<TableSourceItem>(statement.FromClause.TableSources[0]);
        Assert.Equal("users", table.Identifier.ToString());
        Assert.NotNull(table.Alias);
        Assert.Equal("u", table.Alias.ToString());
        
        var columnList = Assert.IsType<ColumnItemList>(statement.SelectorList);
        var col1 = Assert.IsType<ColumnSourceIdentifier>(columnList.Columns[0].Source);
        Assert.Equal("u.id", col1.Identifier.ToString());
    }

    [Fact]
    public void ParsedJoinShouldHaveCorrectStructure()
    {
        var result = SqlParser.Parse("SELECT u.id, o.amount FROM users u JOIN orders o ON u.id = o.user_id");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.FromClause);
        Assert.NotNull(statement.FromClause.Joins);
        Assert.Single(statement.FromClause.Joins);
        
        var join = statement.FromClause.Joins[0];
        Assert.Null(join.JoinKind); // Default join (no INNER/LEFT/RIGHT specified)
        Assert.Single(join.Tables);
        Assert.Equal("orders", join.Tables[0].Identifier.ToString());
        Assert.Equal("o", join.Tables[0].Alias?.ToString());
        
        Assert.Single(join.Conditions);
        var condition = join.Conditions[0];
        var leftExpr = Assert.IsType<IdentifierExpression>(condition.Left);
        Assert.Equal("u.id", leftExpr.Identifier.ToString());
        
        var rightExpr = Assert.IsType<IdentifierExpression>(condition.Right);
        Assert.Equal("o.user_id", rightExpr.Identifier.ToString());
    }

    [Fact]
    public void ParsedLeftJoinShouldHaveCorrectJoinKind()
    {
        var result = SqlParser.Parse("SELECT * FROM users u LEFT JOIN orders o ON u.id = o.user_id");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.FromClause?.Joins);
        var join = statement.FromClause.Joins[0];
        Assert.Equal(JoinKind.Left, join.JoinKind);
    }

    [Fact]
    public void ParsedInnerJoinShouldHaveCorrectJoinKind()
    {
        var result = SqlParser.Parse("SELECT * FROM users u INNER JOIN orders o ON u.id = o.user_id");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.FromClause?.Joins);
        var join = statement.FromClause.Joins[0];
        Assert.Equal(JoinKind.Inner, join.JoinKind);
    }

    [Fact]
    public void ParsedGroupByClauseShouldHaveCorrectColumns()
    {
        var result = SqlParser.Parse("SELECT category, COUNT(*) FROM products GROUP BY category");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.GroupByClause);
        Assert.Single(statement.GroupByClause.Columns);
        
        var groupCol = Assert.IsType<ColumnSourceIdentifier>(statement.GroupByClause.Columns[0]);
        Assert.Equal("category", groupCol.Identifier.ToString());
    }

    [Fact]
    public void ParsedHavingClauseShouldHaveCorrectExpression()
    {
        var result = SqlParser.Parse("SELECT category, COUNT(*) FROM products GROUP BY category HAVING COUNT(*) > 10");
        
        Assert.NotNull(result);
        var statement = GetSelectStatement(result);
        
        Assert.NotNull(statement.GroupByClause);
        Assert.NotNull(statement.HavingClause);
        
        var havingExpr = Assert.IsType<BinaryExpression>(statement.HavingClause.Expression);
        Assert.Equal(BinaryOperator.GreaterThan, havingExpr.Operator);
        
        var left = Assert.IsType<FunctionCall>(havingExpr.Left);
        Assert.Equal("COUNT", left.Name.ToString());
        
        var right = Assert.IsType<LiteralExpression>(havingExpr.Right);
        Assert.Equal(10m, right.Value);
    }

    [Fact]
    public void ParsedUnionShouldHaveCorrectStructure()
    {
        var result = SqlParser.Parse("SELECT * FROM users UNION SELECT * FROM customers");
        
        Assert.NotNull(result);
        Assert.Single(result.Statements);
        
        var statementLine = result.Statements[0];
        Assert.Equal(2, statementLine.UnionStatements.Count);
        
        var first = statementLine.UnionStatements[0];
        Assert.NotNull(first.UnionClause);
        Assert.False(first.UnionClause.IsAll);
        
        var second = statementLine.UnionStatements[1];
        Assert.Null(second.UnionClause);
    }

    [Fact]
    public void ParsedUnionAllShouldHaveIsAllSetToTrue()
    {
        var result = SqlParser.Parse("SELECT * FROM users UNION ALL SELECT * FROM customers");
        
        Assert.NotNull(result);
        var statementLine = result.Statements[0];
        
        var first = statementLine.UnionStatements[0];
        Assert.NotNull(first.UnionClause);
        Assert.True(first.UnionClause.IsAll);
    }

    [Fact]
    public void ParsedCommonTableExpressionShouldHaveCorrectStructure()
    {
        var result = SqlParser.Parse("WITH cte AS (SELECT * FROM users) SELECT * FROM cte");
        
        Assert.NotNull(result);
        Assert.Single(result.Statements);
        
        var statementLine = result.Statements[0];
        Assert.Single(statementLine.UnionStatements);
        var unionStatement = statementLine.UnionStatements[0];
        var statement = unionStatement.Statement;
        
        Assert.NotNull(statement.WithClause);
        Assert.Single(statement.WithClause.CTEs);
        
        var cte = statement.WithClause.CTEs[0];
        Assert.Equal("cte", cte.Name);
        Assert.Null(cte.ColumnNames);
        Assert.Single(cte.Query);
        
        // Verify the CTE query itself
        var cteQuery = cte.Query[0].Statement.SelectStatement;
        Assert.IsType<StarSelector>(cteQuery.SelectorList);
        Assert.NotNull(cteQuery.FromClause);
        var cteTable = Assert.IsType<TableSourceItem>(cteQuery.FromClause.TableSources[0]);
        Assert.Equal("users", cteTable.Identifier.ToString());
    }

    // Helper method to extract the SelectStatement from the parse result
    private static SelectStatement GetSelectStatement(StatementList result)
    {
        Assert.Single(result.Statements);
        var statementLine = result.Statements[0];
        Assert.Single(statementLine.UnionStatements);
        var unionStatement = statementLine.UnionStatements[0];
        return unionStatement.Statement.SelectStatement;
    }
}
