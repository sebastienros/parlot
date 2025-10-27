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
        // Basic terminals
        var COMMA = Terms.Char(',');
        var DOT = Terms.Char('.');
        var SEMICOLON = Terms.Char(';');
        var LPAREN = Terms.Char('(');
        var RPAREN = Terms.Char(')');
        var AT = Terms.Char('@');
        var STAR = Terms.Char('*');
        var EQ = Terms.Char('=');

        // Keywords
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
        var numberLiteral = Terms.Decimal().Then<Expression>(d => new LiteralExpression<decimal>(d));

        var stringLiteral = Terms.String(StringLiteralQuotes.Single)
            .Then<Expression>(s => new LiteralExpression<string>(s.Span.ToString()));

        var booleanLiteral = TRUE.Then<Expression>(new LiteralExpression<bool>(true))
            .Or(FALSE.Then<Expression>(new LiteralExpression<bool>(false)));

        // Identifiers
        var simpleIdentifier = Terms.Identifier()
            .Or(Between(Terms.Char('['), Literals.NoneOf("]"), Terms.Char(']')))
            .Or(Between(Terms.Char('"'), Literals.NoneOf("\""), Terms.Char('"')));

        var identifier = Separated(DOT, simpleIdentifier)
            .Then(parts => new Identifier(parts.Select(p => p.Span.ToString()).ToArray()));

        // Deferred parsers
        var expression = Deferred<Expression>();
        var selectStatement = Deferred<SelectStatement>();
        var columnItem = Deferred<ColumnItem>();
        var orderByItem = Deferred<OrderByItem>();

        // Expression list
        var expressionList = Separated(COMMA, expression);

        // Function arguments
        var starArg = STAR.Then<FunctionArguments>(_ => new StarArgument());
        var selectArg = selectStatement.Then<FunctionArguments>(s => new SelectStatementArgument(s));
        var exprListArg = expressionList.Then<FunctionArguments>(exprs => new ExpressionListArguments(exprs));
        var functionArgs = starArg.Or(selectArg).Or(exprListArg);

        // Function call
        var functionCall = identifier.And(Between(LPAREN, functionArgs, RPAREN))
            .Then<Expression>(x => new FunctionCall(x.Item1, x.Item2));

        // Parameter
        var parameter = AT.And(identifier).Then<Expression>(x => new ParameterExpression(x.Item2));

        // Tuple
        var tuple = Between(LPAREN, expressionList, RPAREN)
            .Then<Expression>(exprs => new TupleExpression(exprs));

        // Parenthesized select
        var parSelectStatement = Between(LPAREN, selectStatement, RPAREN)
            .Then<Expression>(s => new ParenthesizedSelectStatement(s));

        // Basic term
        var identifierExpr = identifier.Then<Expression>(id => new IdentifierExpression(id));

        var term = functionCall
            .Or(parSelectStatement)
            .Or(tuple)
            .Or(booleanLiteral)
            .Or(stringLiteral)
            .Or(numberLiteral)
            .Or(identifierExpr)
            .Or(parameter);

        // Unary expressions
        var unaryMinus = Terms.Char('-').And(term).Then<Expression>(x => new UnaryExpression(UnaryOperator.Minus, x.Item2));
        var unaryPlus = Terms.Char('+').And(term).Then<Expression>(x => new UnaryExpression(UnaryOperator.Plus, x.Item2));
        var unaryNot = NOT.And(term).Then<Expression>(x => new UnaryExpression(UnaryOperator.Not, x.Item2));
        var unaryBitwiseNot = Terms.Char('~').And(term).Then<Expression>(x => new UnaryExpression(UnaryOperator.BitwiseNot, x.Item2));

        var unaryExpr = unaryMinus.Or(unaryPlus).Or(unaryNot).Or(unaryBitwiseNot);
        var primary = unaryExpr.Or(term);

        // Binary operators
        var mulOp = Terms.Char('*').Then(BinaryOperator.Multiply)
            .Or(Terms.Char('/').Then(BinaryOperator.Divide))
            .Or(Terms.Char('%').Then(BinaryOperator.Modulo));

        var addOp = Terms.Char('+').Then(BinaryOperator.Add)
            .Or(Terms.Char('-').Then(BinaryOperator.Subtract));

        var cmpOp = Terms.Text(">=").Then(BinaryOperator.GreaterThanOrEqual)
            .Or(Terms.Text("<=").Then(BinaryOperator.LessThanOrEqual))
            .Or(Terms.Text("<>").Then(BinaryOperator.NotEqual))
            .Or(Terms.Text("!=").Then(BinaryOperator.NotEqualAlt))
            .Or(Terms.Text("!<").Then(BinaryOperator.NotLessThan))
            .Or(Terms.Text("!>").Then(BinaryOperator.NotGreaterThan))
            .Or(Terms.Char('>').Then(BinaryOperator.GreaterThan))
            .Or(Terms.Char('<').Then(BinaryOperator.LessThan))
            .Or(EQ.Then(BinaryOperator.Equal));

        var bitOp = Terms.Char('^').Then(BinaryOperator.BitwiseXor)
            .Or(Terms.Char('&').Then(BinaryOperator.BitwiseAnd))
            .Or(Terms.Char('|').Then(BinaryOperator.BitwiseOr));

        var notLike = NOT.AndSkip(LIKE);
        var likeOp = notLike.Or(LIKE);

        // Build expression with proper precedence
        var multiplicative = primary.LeftAssociative(
            (Terms.Char('*'), (a, b) => new BinaryExpression(a, BinaryOperator.Multiply, b)),
            (Terms.Char('/'), (a, b) => new BinaryExpression(a, BinaryOperator.Divide, b)),
            (Terms.Char('%'), (a, b) => new BinaryExpression(a, BinaryOperator.Modulo, b))
        );

        var additive = multiplicative.LeftAssociative(
            (Terms.Char('+'), (a, b) => new BinaryExpression(a, BinaryOperator.Add, b)),
            (Terms.Char('-'), (a, b) => new BinaryExpression(a, BinaryOperator.Subtract, b))
        );

        var comparisonText = additive.LeftAssociative(
            (Terms.Text(">="), (a, b) => new BinaryExpression(a, BinaryOperator.GreaterThanOrEqual, b)),
            (Terms.Text("<="), (a, b) => new BinaryExpression(a, BinaryOperator.LessThanOrEqual, b)),
            (Terms.Text("<>"), (a, b) => new BinaryExpression(a, BinaryOperator.NotEqual, b)),
            (Terms.Text("!="), (a, b) => new BinaryExpression(a, BinaryOperator.NotEqualAlt, b)),
            (Terms.Text("!<"), (a, b) => new BinaryExpression(a, BinaryOperator.NotLessThan, b)),
            (Terms.Text("!>"), (a, b) => new BinaryExpression(a, BinaryOperator.NotGreaterThan, b))
        );

        var comparisonChar = comparisonText.LeftAssociative(
            (Terms.Char('>'), (a, b) => new BinaryExpression(a, BinaryOperator.GreaterThan, b)),
            (Terms.Char('<'), (a, b) => new BinaryExpression(a, BinaryOperator.LessThan, b)),
            (EQ, (a, b) => new BinaryExpression(a, BinaryOperator.Equal, b))
        );

        var comparison = comparisonChar.LeftAssociative(
            (notLike, (a, b) => new BinaryExpression(a, BinaryOperator.NotLike, b)),
            (LIKE, (a, b) => new BinaryExpression(a, BinaryOperator.Like, b))
        );

        var bitwise = comparison.LeftAssociative(
            (Terms.Char('^'), (a, b) => new BinaryExpression(a, BinaryOperator.BitwiseXor, b)),
            (Terms.Char('&'), (a, b) => new BinaryExpression(a, BinaryOperator.BitwiseAnd, b)),
            (Terms.Char('|'), (a, b) => new BinaryExpression(a, BinaryOperator.BitwiseOr, b))
        );

        var andExpr = bitwise.LeftAssociative(
            (AND, (a, b) => new BinaryExpression(a, BinaryOperator.And, b))
        );

        var orExpr = andExpr.LeftAssociative(
            (OR, (a, b) => new BinaryExpression(a, BinaryOperator.Or, b))
        );

        // BETWEEN and IN expressions
        var betweenExpr = andExpr.And(NOT.Optional()).AndSkip(BETWEEN).And(bitwise).AndSkip(AND).And(bitwise)
            .Then<Expression>(result =>
            {
                var (expr, notKeyword, lower, upper) = result;
                return new BetweenExpression(expr, lower, upper, notKeyword.Any());
            });

        var inExpr = andExpr.And(NOT.Optional()).AndSkip(IN).AndSkip(LPAREN).And(expressionList).AndSkip(RPAREN)
            .Then<Expression>(result =>
            {
                var (expr, notKeyword, values) = result;
                return new InExpression(expr, values, notKeyword.Any());
            });

        expression.Parser = betweenExpr.Or(inExpr).Or(orExpr);

        // Column source
        var columnSourceId = identifier.Then<ColumnSource>(id => new ColumnSourceIdentifier(id));

        // Deferred for OVER clause components
        var columnItemList = Separated(COMMA, columnItem.Or(STAR.Then(new ColumnItem(new ColumnSourceIdentifier(new Identifier("*")), null))));
        var orderByList = Separated(COMMA, orderByItem);

        var orderByClause = ORDER.AndSkip(BY).And(orderByList)
            .Then(x => new OrderByClause(x.Item2));

        var partitionBy = PARTITION.AndSkip(BY).And(columnItemList)
            .Then(x => new PartitionByClause(x.Item2));

        var overClause = OVER.AndSkip(LPAREN).And(partitionBy.Optional()).And(orderByClause.Optional()).AndSkip(RPAREN)
            .Then(result =>
            {
                // OVER.AndSkip(lparen).And(partitionBy.Optional()).And(orderByClause.Optional()).AndSkip(rparen)
                // Result is (TextSpan (OVER), IReadOnlyList<PartitionByClause>, IReadOnlyList<OrderByClause>)
                var partition = result.Item2;
                var orderBy = result.Item3;
                return new OverClause(
                    partition.Count > 0 ? (PartitionByClause?)partition[0] : null,
                    orderBy.Count > 0 ? (OrderByClause?)orderBy[0] : null
                );
            });

        var columnSourceFunc = functionCall.And(overClause.Optional())
            .Then<ColumnSource>(result =>
            {
                // functionCall, overClause.Optional() -> 2 items
                var func = (FunctionCall)result.Item1;
                var over = result.Item2.Count > 0 ? result.Item2[0] : (OverClause?)null;
                return new ColumnSourceFunction(func, over);
            });

        var columnSource = columnSourceFunc.Or(columnSourceId);

        // Column item with alias
        var columnAlias = AS.SkipAnd(identifier);

        columnItem.Parser = columnSource.And(columnAlias.Optional())
            .Then(result =>
            {
                // columnSource, columnAlias.Optional() -> 2 items
                var source = result.Item1;
                var alias = result.Item2.Count > 0 ? result.Item2[0] : null;
                return new ColumnItem(source, alias);
            });

        // Table source
        var tableAlias = AS.SkipAnd(identifier);

        var tableSourceItem = identifier.And(tableAlias.Optional())
            .Then(result =>
            {
                // identifier, tableAlias.Optional() -> 2 items
                var id = result.Item1;
                var alias = result.Item2.Count > 0 ? result.Item2[0] : null;
                return new TableSourceItem(id, alias);
            });

        // Deferred union statement list for subqueries
        var unionStatementList = Deferred<IReadOnlyList<UnionStatement>>();

        var tableSourceSubQuery = LPAREN.And(unionStatementList).AndSkip(RPAREN).AndSkip(AS).And(simpleIdentifier)
            .Then<TableSource>(result =>
            {
                // lparen, unionStatementList, simpleIdentifier -> 3 items (rparen and AS skipped)
                var query = result.Item2;
                var alias = result.Item3.Span.ToString();
                return new TableSourceSubQuery(query, alias);
            });

        var tableSourceItemAsTableSource = tableSourceItem.Then<TableSource>(t => t);
        var tableSource = tableSourceSubQuery.Or(tableSourceItemAsTableSource);
        var tableSourceList = Separated(COMMA, tableSource);

        // Join
        var joinKind = INNER.Then(JoinKind.Inner)
            .Or(LEFT.Then(JoinKind.Left))
            .Or(RIGHT.Then(JoinKind.Right));

        var joinCondition = ON.SkipAnd(bitwise);

        var joinConditions = Separated(AND, joinCondition);
        var tableSourceItemList = Separated(COMMA, tableSourceItem);

        var joinStatement = joinKind.Optional().AndSkip(JOIN).And(tableSourceItemList).And(andExpr)
            .Then(result =>
            {
                // joinKind.Optional(), tableSourceItemList, joinConditions -> 3 items (JOIN and ON skipped)
                var kind = result.Item1.Count > 0 ? result.Item1[0] : JoinKind.None;
                var tables = result.Item2;
                var conditions = result.Item3;
                return new JoinStatement(tables, conditions, kind);
            });

        var joins = ZeroOrMany(joinStatement);

        // FROM clause
        var fromClause = FROM.SkipAnd(tableSourceList).And(joins)
            .Then(result =>
            {
                // FROM, tableSourceList, joins -> 3 items
                var tables = result.Item1;
                var joinList = result.Item2;
                return new FromClause(tables, joinList.Any() ? joinList : null);
            });

        // WHERE clause
        var whereClause = WHERE.And(expression).Then(x => new WhereClause(x.Item2));

        // GROUP BY clause
        var columnSourceList = Separated(COMMA, columnSource);
        var groupByClause = GROUP.AndSkip(BY).And(columnSourceList)
            .Then(x => new GroupByClause(x.Item2));

        // HAVING clause
        var havingClause = HAVING.And(expression).Then(x => new HavingClause(x.Item2));

        // ORDER BY item
        var orderDirection = ASC.Then(OrderDirection.Asc).Or(DESC.Then(OrderDirection.Desc));

        orderByItem.Parser = identifier.And(orderDirection.Optional())
            .Then(result =>
            {
                // identifier, orderDirection.Optional() -> 2 items
                var id = result.Item1;
                var dir = result.Item2.Count > 0 ? result.Item2[0] : (OrderDirection?)null;
                return new OrderByItem(id, dir);
            });

        // LIMIT and OFFSET clauses
        var limitClause = LIMIT.And(expression).Then(x => new LimitClause(x.Item2));
        var offsetClause = OFFSET.And(expression).Then(x => new OffsetClause(x.Item2));

        // SELECT statement
        var selectRestriction = ALL.Then(SelectRestriction.All).Or(DISTINCT.Then(SelectRestriction.Distinct));

        selectStatement.Parser = SELECT
            .SkipAnd(selectRestriction.Optional())
            .And(columnItemList)
            .And(fromClause.Optional())
            .And(whereClause.Optional())
            .And(groupByClause.Optional())
            .And(havingClause.Optional())
            .And(orderByClause.Optional())
            .And(limitClause.Optional())
            .And(offsetClause.Optional())
            .Then(result =>
            {
                var ((restriction, columns, from, where, groupBy, having, orderBy), limit, offset) = result;

                return new SelectStatement(
                    columns,
                    restriction.Count > 0 ? restriction[0] : null,
                    from.Count > 0 ? from[0] : null,
                    where.Count > 0 ? where[0] : null,
                    groupBy.Count > 0 ? groupBy[0] : null,
                    having.Count > 0 ? having[0] : null,
                    orderBy.Count > 0 ? orderBy[0] : null,
                    limit.Count > 0 ? limit[0] : null,
                    offset.Count > 0 ? offset[0] : null
                );
            });

        // WITH clause (CTEs)
        var columnNames = Separated(COMMA, simpleIdentifier)
            .Then(names => names.Select(n => n.Span.ToString()).ToArray());

        var cteColumnList = Between(LPAREN, columnNames, RPAREN);

        var cte = simpleIdentifier
            .And(cteColumnList.Optional())
            .AndSkip(AS)
            .And(Between(LPAREN, unionStatementList, RPAREN))
            .Then(result =>
            {
                // simpleIdentifier, cteColumnList.Optional(), unionStatementList -> 3 items (AS skipped)
                var name = result.Item1.Span.ToString();
                var columns = result.Item2.Count > 0 ? result.Item2[0] : null;
                var query = result.Item3;
                return new CommonTableExpression(name, query, columns);
            });

        var cteList = Separated(COMMA, cte);
        var withClause = WITH.And(cteList)
            .Then(x => new WithClause(x.Item2));

        // UNION
        var unionClause = UNION.And(ALL.Optional())
            .Then(x => new UnionClause(x.Item2.Any()));

        // Statement
        var statement = withClause.Optional().And(selectStatement)
            .Then(result =>
            {
                // withClause.Optional(), selectStatement -> 2 items
                var with = result.Item1.Count > 0 ? (WithClause?)result.Item1[0] : null;
                var select = result.Item2;
                return new Statement(select, with);
            });

        var unionStatement = statement.And(unionClause.Optional())
            .Then(result =>
            {
                // statement, unionClause.Optional() -> 2 items
                var stmt = result.Item1;
                var union = result.Item2.Count > 0 ? (UnionClause?)result.Item2[0] : null;
                return new UnionStatement(stmt, union);
            });

        unionStatementList.Parser = OneOrMany(unionStatement)
            .Then<IReadOnlyList<UnionStatement>>(statements => statements);

        // Statement line
        var statementLine = unionStatementList.And(SEMICOLON.Optional())
            .Then(x => new StatementLine(x.Item1));

        // Statement list
        var statementList = OneOrMany(statementLine)
            .Then(statements => new StatementList(statements));

        Statements = statementList;
    }

    public static StatementList? Parse(string input)
    {
        if (Statements.TryParse(input, out var result, out var error))
        {
            return result;
        }
        throw new InvalidOperationException($"Could not parse SQL: {error}");
        // return null;
    }
}
