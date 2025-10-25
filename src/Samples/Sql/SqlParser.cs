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
        var comma = Terms.Char(',');
        var dot = Terms.Char('.');
        var semicolon = Terms.Char(';');
        var lparen = Terms.Char('(');
        var rparen = Terms.Char(')');
        var at = Terms.Char('@');
        var star = Terms.Char('*');
        var eq = Terms.Char('=');

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
        var numberLiteral = Terms.Decimal().Then<Expression>(d => new LiteralExpression(d));

        var stringLiteral = Terms.String(StringLiteralQuotes.Single)
            .Then<Expression>(s => new LiteralExpression(s.Span.ToString()));

        var booleanLiteral = TRUE.Or(FALSE)
            .Then<Expression>(b => new BooleanLiteral(b.ToString().Equals("TRUE", StringComparison.OrdinalIgnoreCase)));

        // Identifiers
        var simpleIdentifier = Terms.Identifier()
            .Or(Between(Terms.Char('['), Terms.Pattern(c => c != ']'), Terms.Char(']')))
            .Or(Between(Terms.Char('"'), Terms.Pattern(c => c != '"'), Terms.Char('"')));

        var identifier = Separated(dot, simpleIdentifier)
            .Then(parts => new Identifier(parts.Select(p => p.Span.ToString()).ToArray()));

        // Deferred parsers
        var expression = Deferred<Expression>();
        var selectStatement = Deferred<SelectStatement>();
        var columnItem = Deferred<ColumnItem>();
        var orderByItem = Deferred<OrderByItem>();

        // Expression list
        var expressionList = Separated(comma, expression);

        // Function arguments
        var starArg = star.Then<FunctionArguments>(_ => new StarArgument());
        var selectArg = selectStatement.Then<FunctionArguments>(s => new SelectStatementArgument(s));
        var exprListArg = expressionList.Then<FunctionArguments>(exprs => new ExpressionListArguments(exprs.ToArray()));
        var functionArgs = starArg.Or(selectArg).Or(exprListArg);

        // Function call
        var functionCall = identifier.And(Between(lparen, functionArgs, rparen))
            .Then<Expression>(x => new FunctionCall(x.Item1, x.Item2));

        // Parameter
        var parameter = at.And(identifier).Then<Expression>(x => new ParameterExpression(x.Item2));

        // Tuple
        var tuple = Between(lparen, expressionList, rparen)
            .Then<Expression>(exprs => new TupleExpression(exprs.ToArray()));

        // Parenthesized select
        var parSelectStatement = Between(lparen, selectStatement, rparen)
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
            .Or(eq.Then(_ => BinaryOperator.Equal));

        var bitOp = Terms.Char('^').Then(_ => BinaryOperator.BitwiseXor)
            .Or(Terms.Char('&').Then(_ => BinaryOperator.BitwiseAnd))
            .Or(Terms.Char('|').Then(_ => BinaryOperator.BitwiseOr));

        var notLike = NOT.And(SkipWhiteSpace(LIKE)).Then(_ => BinaryOperator.NotLike);
        var likeOp = notLike.Or(LIKE.Then(_ => BinaryOperator.Like));

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

        var comparison = additive.LeftAssociative(
            (Terms.Text(">="), (a, b) => new BinaryExpression(a, BinaryOperator.GreaterThanOrEqual, b)),
            (Terms.Text("<="), (a, b) => new BinaryExpression(a, BinaryOperator.LessThanOrEqual, b)),
            (Terms.Text("<>"), (a, b) => new BinaryExpression(a, BinaryOperator.NotEqual, b)),
            (Terms.Text("!="), (a, b) => new BinaryExpression(a, BinaryOperator.NotEqualAlt, b)),
            (Terms.Text("!<"), (a, b) => new BinaryExpression(a, BinaryOperator.NotLessThan, b)),
            (Terms.Text("!>"), (a, b) => new BinaryExpression(a, BinaryOperator.NotGreaterThan, b)),
            (Terms.Char('>'), (a, b) => new BinaryExpression(a, BinaryOperator.GreaterThan, b)),
            (Terms.Char('<'), (a, b) => new BinaryExpression(a, BinaryOperator.LessThan, b)),
            (eq, (a, b) => new BinaryExpression(a, BinaryOperator.Equal, b)),
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
        var betweenExpr = andExpr.And(NOT.Optional()).And(BETWEEN).And(andExpr).And(AND).And(andExpr)
            .Then<Expression>(result =>
            {
                var expr = result.Item1.Item1.Item1.Item1.Item1;
                var notKeyword = result.Item1.Item1.Item1.Item1.Item2;
                var lower = result.Item1.Item1.Item2;
                var upper = result.Item2;
                return new BetweenExpression(expr, lower, upper, notKeyword.Any());
            });

        var inExpr = andExpr.And(NOT.Optional()).And(IN).And(lparen).And(expressionList).And(rparen)
            .Then<Expression>(result =>
            {
                var expr = result.Item1.Item1.Item1.Item1.Item1;
                var notKeyword = result.Item1.Item1.Item1.Item1.Item2;
                var values = result.Item1.Item2;
                return new InExpression(expr, values.ToArray(), notKeyword.Any());
            });

        expression.Parser = betweenExpr.Or(inExpr).Or(orExpr);

        // Column source
        var columnSourceId = identifier.Then<ColumnSource>(id => new ColumnSourceIdentifier(id));

        // Deferred for OVER clause components
        var columnItemList = Separated(comma, columnItem);
        var orderByList = Separated(comma, orderByItem);

        var orderByClause = ORDER.And(BY).And(orderByList)
            .Then(x => new OrderByClause((IReadOnlyList<OrderByItem>)x.Item2.ToArray()));

        var partitionBy = PARTITION.And(BY).And(columnItemList)
            .Then(x => new PartitionByClause((IReadOnlyList<ColumnItem>)x.Item2.ToArray()));

        var overClause = OVER.And(lparen).And(partitionBy.Optional()).And(orderByClause.Optional()).And(rparen)
            .Then(result =>
            {
                var partition = result.Item1.Item1.Item2;
                var orderBy = result.Item1.Item2;
                return new OverClause(
                    partition.Count > 0 ? partition[0] : null,
                    orderBy.Count > 0 ? orderBy[0] : null
                );
            });

        var columnSourceFunc = functionCall.And(overClause.Optional())
            .Then<ColumnSource>(result =>
            {
                var func = (FunctionCall)result.Item1;
                var over = result.Item2.Count > 0 ? result.Item2[0] : null;
                return new ColumnSourceFunction(func, over);
            });

        var columnSource = columnSourceFunc.Or(columnSourceId);

        // Column item with optional alias
        var columnAlias = AS.Optional().And(identifier).Then(x => x.Item2);

        columnItem.Parser = columnSource.And(columnAlias.Optional())
            .Then(result =>
            {
                var source = result.Item1;
                var alias = result.Item2.Count > 0 ? result.Item2[0] : null;
                return new ColumnItem(source, alias);
            });

        // Selector list
        var starSelector = star.Then<SelectorList>(_ => new StarSelector());
        var columnListSelector = columnItemList.Then<SelectorList>(items => new ColumnItemList(items.ToArray()));
        var selectorList = starSelector.Or(columnListSelector);

        // Table source
        var tableAlias = AS.Optional().And(identifier).Then(x => x.Item2);

        var tableSourceItem = identifier.And(tableAlias.Optional())
            .Then(result =>
            {
                var id = result.Item1;
                var alias = result.Item2.Count > 0 ? result.Item2[0] : null;
                return new TableSourceItem(id, alias);
            });

        // Deferred union statement list for subqueries
        var unionStatementList = Deferred<IReadOnlyList<UnionStatement>>();

        var tableSourceSubQuery = lparen.And(unionStatementList).And(rparen).And(AS).And(simpleIdentifier)
            .Then<TableSource>(result =>
            {
                var query = result.Item1.Item1.Item1.Item2;
                var alias = result.Item2.Span.ToString();
                return new TableSourceSubQuery(query, alias);
            });

        var tableSourceItemAsTableSource = tableSourceItem.Then<TableSource>(t => t);
        var tableSource = tableSourceSubQuery.Or(tableSourceItemAsTableSource);
        var tableSourceList = Separated(comma, tableSource);

        // Join
        var joinKind = INNER.Then(_ => JoinKind.Inner)
            .Or(LEFT.Then(_ => JoinKind.Left))
            .Or(RIGHT.Then(_ => JoinKind.Right));

        var joinCondition = expression.And(eq).And(expression)
            .Then(x => new JoinCondition(x.Item1.Item1, x.Item2));

        var joinConditions = Separated(AND, joinCondition);
        var tableSourceItemList = Separated(comma, tableSourceItem);

        var joinStatement = joinKind.Optional().And(JOIN).And(tableSourceItemList).And(ON).And(joinConditions)
            .Then(result =>
            {
                var kind = result.Item1.Item1.Item1.Item1.Count > 0 ? result.Item1.Item1.Item1.Item1[0] : (JoinKind?)null;
                var tables = result.Item1.Item1.Item2;
                var conditions = result.Item2;
                return new JoinStatement(tables.ToArray(), conditions.ToArray(), kind);
            });

        var joins = ZeroOrMany(joinStatement);

        // FROM clause
        var fromClause = FROM.And(tableSourceList).And(joins)
            .Then(result =>
            {
                var tables = result.Item1.Item2;
                var joinList = result.Item2;
                return new FromClause(tables.ToArray(), joinList.Any() ? joinList.ToArray() : null);
            });

        // WHERE clause
        var whereClause = WHERE.And(expression).Then(x => new WhereClause(x.Item2));

        // GROUP BY clause
        var columnSourceList = Separated(comma, columnSource);
        var groupByClause = GROUP.And(BY).And(columnSourceList)
            .Then(x => new GroupByClause(x.Item2.ToArray()));

        // HAVING clause
        var havingClause = HAVING.And(expression).Then(x => new HavingClause(x.Item2));

        // ORDER BY item
        var orderDirection = ASC.Then(_ => OrderDirection.Asc).Or(DESC.Then(_ => OrderDirection.Desc));

        orderByItem.Parser = identifier.And(orderDirection.Optional())
            .Then(result =>
            {
                var id = result.Item1;
                var dir = result.Item2.Count > 0 ? result.Item2[0] : (OrderDirection?)null;
                return new OrderByItem(id, dir);
            });

        // LIMIT and OFFSET clauses
        var limitClause = LIMIT.And(expression).Then(x => new LimitClause(x.Item2));
        var offsetClause = OFFSET.And(expression).Then(x => new OffsetClause(x.Item2));

        // SELECT statement
        var selectRestriction = ALL.Then(_ => SelectRestriction.All).Or(DISTINCT.Then(_ => SelectRestriction.Distinct));

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
            .Then(result =>
            {
                // Unwrap the nested tuple structure carefully
                var rest9 = result.Item1;
                var rest8 = rest9.Item1;
                var rest7 = rest8.Item1;
                var rest6 = rest7.Item1;
                var rest5 = rest6.Item1;
                var rest4 = rest5.Item1;
                var rest3 = rest4.Item1;
                var rest2 = rest3.Item1;

                var restriction = rest2.Item2.Count > 0 ? (SelectRestriction?)rest2.Item2[0] : null;
                var selector = rest3.Item2;
                var from = rest4.Item2.Count > 0 ? rest4.Item2[0] : null;
                var where = rest5.Item2.Count > 0 ? rest5.Item2[0] : null;
                var groupBy = rest6.Item2.Count > 0 ? rest6.Item2[0] : null;
                var having = rest7.Item2.Count > 0 ? rest7.Item2[0] : null;
                var orderBy = rest8.Item2.Count > 0 ? (OrderByClause?)rest8.Item2[0] : null;
                var limit = rest9.Item2.Count > 0 ? rest9.Item2[0] : null;
                var offset = result.Item2.Count > 0 ? result.Item2[0] : null;

                return new SelectStatement(selector, restriction, from, where, groupBy, having, orderBy, limit, offset);
            });

        // WITH clause (CTEs)
        var columnNames = Separated(comma, simpleIdentifier)
            .Then(names => names.Select(n => n.Span.ToString()).ToArray());

        var cteColumnList = Between(lparen, columnNames, rparen);

        var cte = simpleIdentifier
            .And(cteColumnList.Optional())
            .And(AS)
            .And(lparen)
            .And(unionStatementList)
            .And(rparen)
            .Then(result =>
            {
                // Unwrap: (((((<TextSpan>, <IReadOnlyList<string[]>>), <TextSpan>), <char>), <IReadOnlyList<UnionStatement>>), <char>)
                var rest5 = result.Item1; // ((((<TextSpan>, <IReadOnlyList<string[]>>), <TextSpan>), <char>), <IReadOnlyList<UnionStatement>>)
                var rest4 = rest5.Item1; // (((<TextSpan>, <IReadOnlyList<string[]>>), <TextSpan>), <char>)
                var rest3 = rest4.Item1; // ((<TextSpan>, <IReadOnlyList<string[]>>), <TextSpan>)
                var rest2 = rest3.Item1; // (<TextSpan>, <IReadOnlyList<string[]>>)
                
                var name = rest2.Item1.Span.ToString();
                var columns = rest2.Item2.Count > 0 ? rest2.Item2[0] : null;
                var query = rest5.Item2;
                return new CommonTableExpression(name, query, columns);
            });

        var cteList = Separated(comma, cte);
        var withClause = WITH.And(cteList)
            .Then(x => new WithClause((IReadOnlyList<CommonTableExpression>)(object)x.Item2.ToArray()));

        // UNION
        var unionClause = UNION.And(ALL.Optional())
            .Then(x => new UnionClause(x.Item2.Any()));

        // Statement
        var statement = withClause.Optional().And(selectStatement)
            .Then(result =>
            {
                var with = result.Item1.Count > 0 ? (WithClause?)result.Item1[0] : null;
                var select = result.Item2;
                return new Statement(select, with);
            });

        var unionStatement = statement.And(unionClause.Optional())
            .Then(result =>
            {
                var stmt = result.Item1;
                var union = result.Item2.Count > 0 ? (UnionClause?)result.Item2[0] : null;
                return new UnionStatement(stmt, union);
            });

        unionStatementList.Parser = OneOrMany(unionStatement)
            .Then<IReadOnlyList<UnionStatement>>(statements => (IReadOnlyList<UnionStatement>)(object)statements.ToArray());

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
