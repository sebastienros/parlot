#nullable enable

using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests.Sql;

public class SqlParser
{
    public static readonly Parser<StatementList> Statements;

    static SqlParser()
    {
        // Character parsers
        var comma = Terms.Char(',');
        var dot = Terms.Char('.');
        var semicolon = Terms.Char(';');
        var lparen = Terms.Char('(');
        var rparen = Terms.Char(')');
        var at = Terms.Char('@');
        var star = Terms.Char('*');

        // Keywords (case-insensitive)
        var SELECT = Terms.Text("SELECT", caseInsensitive: true);
        var FROM = Terms.Text("FROM", caseInsensitive: true);
        var WHERE = Terms.Text("WHERE", caseInsensitive: true);
        var AS = Terms.Text("AS", caseInsensitive: true);
        var JOIN = Terms.Text("JOIN", caseInsensitive: true);
        var INNER = Terms.Text("INNER", caseInsensitive: true);
        var LEFT = Terms.Text("LEFT", caseInsensitive: true);
        var RIGHT = Terms.Text("RIGHT", caseInsensitive: true);
        var ON = Terms.Text("ON", caseInsensitive: true);
        var GROUP = Terms.Text("GROUP", caseInsensitive: true);
        var BY = Terms.Text("BY", caseInsensitive: true);
        var HAVING = Terms.Text("HAVING", caseInsensitive: true);
        var ORDER = Terms.Text("ORDER", caseInsensitive: true);
        var ASC = Terms.Text("ASC", caseInsensitive: true);
        var DESC = Terms.Text("DESC", caseInsensitive: true);
        var LIMIT = Terms.Text("LIMIT", caseInsensitive: true);
        var OFFSET = Terms.Text("OFFSET", caseInsensitive: true);
        var UNION = Terms.Text("UNION", caseInsensitive: true);
        var ALL = Terms.Text("ALL", caseInsensitive: true);
        var DISTINCT = Terms.Text("DISTINCT", caseInsensitive: true);
        var WITH = Terms.Text("WITH", caseInsensitive: true);
        var AND = Terms.Text("AND", caseInsensitive: true);
        var OR = Terms.Text("OR", caseInsensitive: true);
        var NOT = Terms.Text("NOT", caseInsensitive: true);
        var BETWEEN = Terms.Text("BETWEEN", caseInsensitive: true);
        var IN = Terms.Text("IN", caseInsensitive: true);
        var LIKE = Terms.Text("LIKE", caseInsensitive: true);
        var TRUE = Terms.Text("TRUE", caseInsensitive: true);
        var FALSE = Terms.Text("FALSE", caseInsensitive: true);
        var OVER = Terms.Text("OVER", caseInsensitive: true);
        var PARTITION = Terms.Text("PARTITION", caseInsensitive: true);

        // Literals
        var numberLiteral = Terms.Decimal()
            .Then<Expression>(d => new LiteralExpression(d));

        var stringLiteral = Terms.String(StringLiteralQuotes.Single)
            .Then<Expression>(s => new LiteralExpression(s.Span.ToString()));

        var booleanLiteral = TRUE.Or(FALSE)
            .Then<Expression>(b => new BooleanLiteral(b.ToString().Equals("TRUE", StringComparison.OrdinalIgnoreCase)));

        // Identifier - simple name or quoted name
        var simpleIdentifier = Terms.Identifier()
            .Or(Between(Terms.Char('['), Terms.Pattern(c => c != ']'), Terms.Char(']')))
            .Or(Between(Terms.Char('"'), Terms.Pattern(c => c != '"'), Terms.Char('"')));

        // Dotted identifier (e.g. schema.table.column)
        var identifier = Separated(dot, simpleIdentifier)
            .Then(parts => new Identifier(parts.Select(p => p.Span.ToString()).ToArray()));

        // Deferred parsers for recursive grammar
        var expression = Deferred<Expression>();
        var selectStatement = Deferred<SelectStatement>();

        // Expression list for function arguments, tuples, etc.
        var expressionList = Separated(comma, expression);

        // Function arguments
        var starArg = star.Then<FunctionArguments>(_ => new StarArgument());

        var selectArg = selectStatement.Then<FunctionArguments>(s => new SelectStatementArgument(s));

        var exprListArg = expressionList.Then<FunctionArguments>(exprs => new ExpressionListArguments(exprs.ToArray()));

        var functionArgs = starArg.Or(selectArg).Or(exprListArg);

        // Function call
        var functionCall = identifier.And(lparen).And(functionArgs).And(rparen)
            .Then<Expression>(x => new FunctionCall(x.Item1.Item1.Item1, x.Item1.Item2));

        // Parameter (@param or @param:value)
        var parameter = at.And(identifier)
            .Then<Expression>(x => new ParameterExpression(x.Item2));

        // Tuple (expression list in parentheses)
        var tuple = Between(lparen, expressionList, rparen)
            .Then<Expression>(exprs => new TupleExpression(exprs.ToArray()));

        // Parenthesized select statement
        var parSelectStatement = Between(lparen, selectStatement, rparen)
            .Then<Expression>(s => new ParenthesizedSelectStatement(s));

        // Identifier as expression
        var identifierExpr = identifier.Then<Expression>(id => new IdentifierExpression(id));

        // Term (basic expression)
        var term = functionCall
            .Or(parSelectStatement)
            .Or(tuple)
            .Or(booleanLiteral)
            .Or(stringLiteral)
            .Or(numberLiteral)
            .Or(identifierExpr)
            .Or(parameter);

        // Unary expression
        var unaryMinus = Terms.Char('-').And(term)
            .Then<Expression>(x => new UnaryExpression(UnaryOperator.Minus, x.Item2));

        var unaryPlus = Terms.Char('+').And(term)
            .Then<Expression>(x => new UnaryExpression(UnaryOperator.Plus, x.Item2));

        var unaryNot = NOT.And(term)
            .Then<Expression>(x => new UnaryExpression(UnaryOperator.Not, x.Item2));

        var unaryBitwiseNot = Terms.Char('~').And(term)
            .Then<Expression>(x => new UnaryExpression(UnaryOperator.BitwiseNot, x.Item2));

        var unaryExpr = unaryMinus.Or(unaryPlus).Or(unaryNot).Or(unaryBitwiseNot);

        // Primary expression
        var primary = unaryExpr.Or(term);

        // Binary operators
        var mulOp = Terms.Char('*').Then(_ => BinaryOperator.Multiply)
            .Or(Terms.Char('/').Then(_ => BinaryOperator.Divide))
            .Or(Terms.Char('%').Then(_ => BinaryOperator.Modulo));

        var addOp = Terms.Char('+').Then(_ => BinaryOperator.Add)
            .Or(Terms.Char('-').Then(_ => BinaryOperator.Subtract));

        var cmpOp = Terms.Text(">=").Then(_ => BinaryOperator.GreaterThanOrEqual)
            .Or(Terms.Text("<=").Then(_ => BinaryOperator.LessThanOrEqual))
            .Or(Terms.Text("<>").Then(_ => BinaryOperator.NotEqual))
            .Or(Terms.Text("!=").Then(_ => BinaryOperator.NotEqualAlt))
            .Or(Terms.Text("!<").Then(_ => BinaryOperator.NotLessThan))
            .Or(Terms.Text("!>").Then(_ => BinaryOperator.NotGreaterThan))
            .Or(Terms.Char('>').Then(_ => BinaryOperator.GreaterThan))
            .Or(Terms.Char('<').Then(_ => BinaryOperator.LessThan))
            .Or(Terms.Char('=').Then(_ => BinaryOperator.Equal));

        var bitOp = Terms.Char('^').Then(_ => BinaryOperator.BitwiseXor)
            .Or(Terms.Char('&').Then(_ => BinaryOperator.BitwiseAnd))
            .Or(Terms.Char('|').Then(_ => BinaryOperator.BitwiseOr));

        var notLike = NOT.And(SkipWhiteSpace(LIKE)).Then(_ => BinaryOperator.NotLike);
        var likeOp = notLike.Or(LIKE.Then(_ => BinaryOperator.Like));

        // Build expression with proper precedence using LeftAssociative
        var multiplicative = primary.LeftAssociative(
            (mulOp, (a, op, b) => new BinaryExpression(a, op, b))
        );

        var additive = multiplicative.LeftAssociative(
            (addOp, (a, op, b) => new BinaryExpression(a, op, b))
        );

        var comparison = additive.LeftAssociative(
            (cmpOp, (a, op, b) => new BinaryExpression(a, op, b)),
            (likeOp, (a, op, b) => new BinaryExpression(a, op, b))
        );

        var bitwise = comparison.LeftAssociative(
            (bitOp, (a, op, b) => new BinaryExpression(a, op, b))
        );

        var andExpr = bitwise.LeftAssociative(
            (AND.Then(_ => BinaryOperator.And), (a, op, b) => new BinaryExpression(a, op, b))
        );

        var orExpr = andExpr.LeftAssociative(
            (OR.Then(_ => BinaryOperator.Or), (a, op, b) => new BinaryExpression(a, op, b))
        );

        // BETWEEN expression
        var betweenExpr = andExpr.And(NOT.Optional()).And(BETWEEN).And(andExpr).And(AND).And(andExpr)
            .Then<Expression>(x =>
            {
                var expr = x.Item1.Item1.Item1.Item1.Item1;
                var isNot = x.Item1.Item1.Item1.Item1.Item2.Length > 0;
                var lower = x.Item1.Item1.Item2;
                var upper = x.Item2;
                return new BetweenExpression(expr, lower, upper, isNot);
            });

        // IN expression
        var inExpr = andExpr.And(NOT.Optional()).And(IN).And(lparen).And(expressionList).And(rparen)
            .Then<Expression>(x =>
            {
                var expr = x.Item1.Item1.Item1.Item1.Item1;
                var isNot = x.Item1.Item1.Item1.Item1.Item2.Length > 0;
                var values = x.Item1.Item2;
                return new InExpression(expr, values.ToArray(), isNot);
            });

        // Final expression (including BETWEEN and IN)
        expression.Parser = betweenExpr.Or(inExpr).Or(orExpr);

        // Column source (identifier or function call with optional OVER clause)
        var columnSourceId = identifier.Then<ColumnSource>(id => new ColumnSourceIdentifier(id));

        // Over clause components
        var columnItem = Deferred<ColumnItem>();
        var columnItemList = Separated(comma, columnItem);

        var orderByItem = Deferred<OrderByItem>();
        var orderByList = Separated(comma, orderByItem);

        var orderByClause = ORDER.And(BY).And(orderByList)
            .Then(x => new OrderByClause(x.Item2.ToArray()));

        var partitionBy = PARTITION.And(BY).And(columnItemList)
            .Then(x => new PartitionByClause(x.Item2.ToArray()));

        var overClause = OVER.And(lparen).And(partitionBy.Optional()).And(orderByClause.Optional()).And(rparen)
            .Then(x => new OverClause(x.Item1.Item1.Item2, x.Item1.Item2));

        var columnSourceFunc = functionCall.And(overClause.Optional())
            .Then<ColumnSource>(x => new ColumnSourceFunction((FunctionCall)x.Item1, x.Item2));

        var columnSource = columnSourceFunc.Or(columnSourceId);

        // Column item with optional alias
        var columnAlias = AS.Optional().And(identifier).Then(x => x.Item2);

        columnItem.Parser = columnSource.And(columnAlias.Optional())
            .Then(x => new ColumnItem(x.Item1, x.Item2));

        // Selector list
        var starSelector = star.Then<SelectorList>(_ => new StarSelector());

        var columnListSelector = columnItemList.Then<SelectorList>(items => new ColumnItemList(items.ToArray()));

        var selectorList = starSelector.Or(columnListSelector);

        // Table source
        var tableAlias = AS.Optional().And(identifier).Then(x => x.Item2);

        var tableSourceItem = identifier.And(tableAlias.Optional())
            .Then(x => new TableSourceItem(x.Item1, x.Item2));

        // Deferred union statement list for subqueries
        var unionStatementList = Deferred<IReadOnlyList<UnionStatement>>();

        var tableSourceSubQuery = lparen.And(unionStatementList).And(rparen).And(AS).And(simpleIdentifier)
            .Then<TableSource>(x => new TableSourceSubQuery(x.Item1.Item1.Item1.Item2, x.Item2.Span.ToString()));

        var tableSourceItemAsTableSource = tableSourceItem.Then<TableSource>(t => t);

        var tableSource = tableSourceSubQuery.Or(tableSourceItemAsTableSource);

        var tableSourceList = Separated(comma, tableSource);

        // Join
        var joinKind = INNER.Then(_ => JoinKind.Inner)
            .Or(LEFT.Then(_ => JoinKind.Left))
            .Or(RIGHT.Then(_ => JoinKind.Right));

        var joinCondition = expression.And(Terms.Char('=')).And(expression)
            .Then(x => new JoinCondition(x.Item1.Item1, x.Item2));

        var joinConditions = Separated(AND, joinCondition);

        var tableSourceItemList = Separated(comma, tableSourceItem);

        var joinStatement = joinKind.Optional().And(JOIN).And(tableSourceItemList).And(ON).And(joinConditions)
            .Then(x => new JoinStatement(x.Item1.Item1.Item1.Item2.ToArray(), x.Item2.ToArray(), x.Item1.Item1.Item1.Item1));

        var joins = ZeroOrMany(joinStatement);

        // FROM clause
        var fromClause = FROM.And(tableSourceList).And(joins)
            .Then(x => new FromClause(x.Item1.Item2.ToArray(), x.Item2.Any() ? x.Item2.ToArray() : null));

        // WHERE clause
        var whereClause = WHERE.And(expression)
            .Then(x => new WhereClause(x.Item2));

        // GROUP BY clause
        var columnSourceList = Separated(comma, columnSource);
        var groupByClause = GROUP.And(BY).And(columnSourceList)
            .Then(x => new GroupByClause(x.Item2.ToArray()));

        // HAVING clause
        var havingClause = HAVING.And(expression)
            .Then(x => new HavingClause(x.Item2));

        // ORDER BY item
        var orderDirection = ASC.Then(_ => OrderDirection.Asc)
            .Or(DESC.Then(_ => OrderDirection.Desc));

        orderByItem.Parser = identifier.And(orderDirection.Optional())
            .Then(x => new OrderByItem(x.Item1, x.Item2));

        // LIMIT clause
        var limitClause = LIMIT.And(expression)
            .Then(x => new LimitClause(x.Item2));

        // OFFSET clause
        var offsetClause = OFFSET.And(expression)
            .Then(x => new OffsetClause(x.Item2));

        // SELECT statement
        var selectRestriction = ALL.Then(_ => SelectRestriction.All)
            .Or(DISTINCT.Then(_ => SelectRestriction.Distinct));

        selectStatement.Parser = SELECT
            .And(selectRestriction.Optional())
            .And(selectorList)
            .And(fromClause.Optional())
            .And(whereClause.Optional())
            .And(groupByClause.Optional())
            .And(havingClause.Optional())
            .And(orderByClause.Optional())
            .And(limitClause.Optional())
            .And(offsetClause.Optional())
            .Then(x => new SelectStatement(
                x.Item1.Item1.Item1.Item1.Item1.Item1.Item1.Item1.Item2,
                x.Item1.Item1.Item1.Item1.Item1.Item1.Item1.Item2,
                x.Item1.Item1.Item1.Item1.Item1.Item1.Item2,
                x.Item1.Item1.Item1.Item1.Item1.Item2,
                x.Item1.Item1.Item1.Item1.Item2,
                x.Item1.Item1.Item1.Item2,
                x.Item1.Item1.Item2,
                x.Item1.Item2,
                x.Item2));

        // WITH clause (CTEs)
        var columnNames = Separated(comma, simpleIdentifier)
            .Then(names => names.Select(n => n.Span.ToString()).ToArray());

        var cteColumnList = lparen.And(columnNames).And(rparen)
            .Then(x => x.Item1.Item2);

        var cte = simpleIdentifier
            .And(cteColumnList.Optional())
            .And(AS)
            .And(lparen)
            .And(unionStatementList)
            .And(rparen)
            .Then(x => new CommonTableExpression(
                x.Item1.Item1.Item1.Item1.Item1.Span.ToString(),
                x.Item1.Item2,
                x.Item1.Item1.Item1.Item1.Item2));

        var cteList = Separated(comma, cte);

        var withClause = WITH.And(cteList)
            .Then(x => new WithClause(x.Item2.ToArray()));

        // UNION
        var unionClause = UNION.And(ALL.Optional())
            .Then(x => new UnionClause(x.Item2.Length > 0));

        // Statement
        var statement = withClause.Optional().And(selectStatement)
            .Then(x => new Statement(x.Item2, x.Item1));

        var unionStatement = statement.And(unionClause.Optional())
            .Then(x => new UnionStatement(x.Item1, x.Item2));

        unionStatementList.Parser = OneOrMany(unionStatement)
            .Then<IReadOnlyList<UnionStatement>>(statements => statements.ToArray());

        // Statement line
        var statementLine = unionStatementList.And(semicolon.Optional())
            .Then(x => new StatementLine(x.Item1));

        // Statement list
        var statementList = OneOrMany(statementLine)
            .Then(statements => new StatementList(statements.ToArray()));

        Statements = statementList;
    }

    public static StatementList? Parse(string input)
    {
        if (Statements.TryParse(input, out var result))
        {
            return result;
        }
        return null;
    }
}
