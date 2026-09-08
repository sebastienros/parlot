#nullable enable

using Parlot.Fluent;
using Parlot.SourceGenerator;
using System.Collections.Generic;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests.Sql;

public partial class SqlParser
{
    [GenerateParser(nameof(TryParse))]
    [IncludeUsings("System.Linq")]
    private static Parser<StatementList> BuildGeneratedParser()
    {
        var COMMA = Terms.Char(',');
        var DOT = Terms.Char('.');
        var SEMICOLON = Terms.Char(';');
        var LPAREN = Terms.Char('(');
        var RPAREN = Terms.Char(')');
        var AT = Terms.Char('@');
        var STAR = Terms.Char('*');
        var EQ = Terms.Char('=');

        var SELECT = Terms.Keyword("SELECT", caseInsensitive: true);
        var FROM = Terms.Keyword("FROM", caseInsensitive: true);
        var WHERE = Terms.Keyword("WHERE", caseInsensitive: true);
        var AS = Terms.Keyword("AS", caseInsensitive: true);
        var JOIN = Terms.Keyword("JOIN", caseInsensitive: true);
        var INNER = Terms.Keyword("INNER", caseInsensitive: true);
        var LEFT = Terms.Keyword("LEFT", caseInsensitive: true);
        var RIGHT = Terms.Keyword("RIGHT", caseInsensitive: true);
        var ON = Terms.Keyword("ON", caseInsensitive: true);
        var GROUP = Terms.Keyword("GROUP", caseInsensitive: true);
        var BY = Terms.Keyword("BY", caseInsensitive: true);
        var HAVING = Terms.Keyword("HAVING", caseInsensitive: true);
        var ORDER = Terms.Keyword("ORDER", caseInsensitive: true);
        var ASC = Terms.Keyword("ASC", caseInsensitive: true);
        var DESC = Terms.Keyword("DESC", caseInsensitive: true);
        var LIMIT = Terms.Keyword("LIMIT", caseInsensitive: true);
        var OFFSET = Terms.Keyword("OFFSET", caseInsensitive: true);
        var UNION = Terms.Keyword("UNION", caseInsensitive: true);
        var ALL = Terms.Keyword("ALL", caseInsensitive: true);
        var DISTINCT = Terms.Keyword("DISTINCT", caseInsensitive: true);
        var WITH = Terms.Keyword("WITH", caseInsensitive: true);
        var AND = Terms.Keyword("AND", caseInsensitive: true);
        var OR = Terms.Keyword("OR", caseInsensitive: true);
        var NOT = Terms.Keyword("NOT", caseInsensitive: true);
        var BETWEEN = Terms.Keyword("BETWEEN", caseInsensitive: true);
        var IN = Terms.Keyword("IN", caseInsensitive: true);
        var LIKE = Terms.Keyword("LIKE", caseInsensitive: true);
        var TRUE = Terms.Keyword("TRUE", caseInsensitive: true);
        var FALSE = Terms.Keyword("FALSE", caseInsensitive: true);
        var OVER = Terms.Keyword("OVER", caseInsensitive: true);
        var PARTITION = Terms.Keyword("PARTITION", caseInsensitive: true);

        var reservedKeyword = OneOf(
            SELECT, FROM, WHERE, AS, JOIN, INNER, LEFT, RIGHT, ON,
            GROUP, BY, HAVING, ORDER, ASC, DESC, LIMIT, OFFSET,
            UNION, ALL, DISTINCT, WITH, AND, OR, NOT, BETWEEN,
            IN, LIKE, TRUE, FALSE, OVER, PARTITION);

        var numberLiteral = Terms.Decimal().Then<Expression>(d => new LiteralExpression<decimal>(d));

        var stringLiteral = Terms.String(StringLiteralQuotes.Single)
            .Then<Expression>(s => new LiteralExpression<string>(s.ToString()));

        var booleanLiteral = TRUE.Then<Expression>(static _ => LiteralExpression<bool>.TRUE)
            .Or(FALSE.Then<Expression>(static _ => LiteralExpression<bool>.FALSE));

        var simpleIdentifier = Terms.Identifier().Then(x => x.ToString())
            .Or(Between(Terms.Char('['), Literals.NoneOf("]"), Terms.Char(']')).Then(x => x.ToString()))
            .Or(Between(Terms.Char('"'), Literals.NoneOf("\""), Terms.Char('"')).Then(x => x.ToString())).Named("SimpleIdentifier");

        var identifier = Separated(DOT, simpleIdentifier).Named("Identifier")
            .Then(parts => new Identifier(parts));

        var identifierNoKeywords = Not(reservedKeyword)
            .SkipAnd(Separated(DOT, simpleIdentifier))
            .Named("IdentiferNoKeywords")
            .Then(static parts => new Identifier(parts));

        var expression = Deferred<Expression>();
        var selectStatement = Deferred<SelectStatement>();
        var columnItem = Deferred<ColumnItem>();
        var orderByItem = Deferred<OrderByItem>();

        var expressionList = Separated(COMMA, expression);

        var starArg = STAR.Then<FunctionArguments>(static _ => StarArgument.Instance);
        var selectArg = selectStatement.Then<FunctionArguments>(static statement => new SelectStatementArgument(statement));
        var exprListArg = expressionList.Then<FunctionArguments>(static expressions => new ExpressionListArguments(expressions));
        var emptyArg = Always<object?>().Then<FunctionArguments>(static _ => EmptyArguments.Instance);
        var functionArgs = starArg.Or(selectArg).Or(exprListArg).Or(emptyArg);

        var functionCall = identifier.And(Between(LPAREN, functionArgs, RPAREN))
            .Then(value => new FunctionCall(value.Item1, value.Item2));

        var tuple = Between(LPAREN, expressionList, RPAREN)
            .Then<Expression>(expressions => new TupleExpression(expressions));

        var parSelectStatement = Between(LPAREN, selectStatement, RPAREN)
            .Then<Expression>(statement => new ParenthesizedSelectStatement(statement));

        var identifierExpr = identifierNoKeywords.Then<Expression>(id => new IdentifierExpression(id));

        var functionCallExpr = identifierNoKeywords.And(Between(LPAREN, functionArgs, RPAREN))
            .Then<Expression>(value => new FunctionCall(value.Item1, value.Item2));

        var termNoParameter = functionCallExpr
            .Or(parSelectStatement)
            .Or(tuple)
            .Or(booleanLiteral)
            .Or(stringLiteral)
            .Or(numberLiteral)
            .Or(identifierExpr);

        var parameter = AT.SkipAnd(identifier)
            .And(Literals.Char(':').SkipAnd(termNoParameter).Optional())
            .Then<Expression>(value => new ParameterExpression(
                value.Item1,
                value.Item2.HasValue ? value.Item2.Value : null));

        var term = termNoParameter.Or(parameter);

        var unaryMinus = Terms.Char('-').And(term)
            .Then<Expression>(value => new UnaryExpression(UnaryOperator.Minus, value.Item2));
        var unaryPlus = Terms.Char('+').And(term)
            .Then<Expression>(value => new UnaryExpression(UnaryOperator.Plus, value.Item2));
        var unaryNot = NOT.And(term)
            .Then<Expression>(value => new UnaryExpression(UnaryOperator.Not, value.Item2));
        var unaryBitwiseNot = Terms.Char('~').And(term)
            .Then<Expression>(value => new UnaryExpression(UnaryOperator.BitwiseNot, value.Item2));

        var unaryExpr = unaryMinus.Or(unaryPlus).Or(unaryNot).Or(unaryBitwiseNot);
        var primary = unaryExpr.Or(term);

        var notLike = NOT.AndSkip(LIKE);

        var multiplicative = primary.LeftAssociative(
            (Terms.Char('*'), (left, right) => new BinaryExpression(left, BinaryOperator.Multiply, right)),
            (Terms.Char('/'), (left, right) => new BinaryExpression(left, BinaryOperator.Divide, right)),
            (Terms.Char('%'), (left, right) => new BinaryExpression(left, BinaryOperator.Modulo, right))
        );

        var additive = multiplicative.LeftAssociative(
            (Terms.Char('+'), (left, right) => new BinaryExpression(left, BinaryOperator.Add, right)),
            (Terms.Char('-'), (left, right) => new BinaryExpression(left, BinaryOperator.Subtract, right))
        );

        var comparisonText = additive.LeftAssociative(
            (Terms.Text(">="), (left, right) => new BinaryExpression(left, BinaryOperator.GreaterThanOrEqual, right)),
            (Terms.Text("<="), (left, right) => new BinaryExpression(left, BinaryOperator.LessThanOrEqual, right)),
            (Terms.Text("<>"), (left, right) => new BinaryExpression(left, BinaryOperator.NotEqual, right)),
            (Terms.Text("!="), (left, right) => new BinaryExpression(left, BinaryOperator.NotEqualAlt, right)),
            (Terms.Text("!<"), (left, right) => new BinaryExpression(left, BinaryOperator.NotLessThan, right)),
            (Terms.Text("!>"), (left, right) => new BinaryExpression(left, BinaryOperator.NotGreaterThan, right))
        );

        var comparisonChar = comparisonText.LeftAssociative(
            (Terms.Char('>'), (left, right) => new BinaryExpression(left, BinaryOperator.GreaterThan, right)),
            (Terms.Char('<'), (left, right) => new BinaryExpression(left, BinaryOperator.LessThan, right)),
            (EQ, (left, right) => new BinaryExpression(left, BinaryOperator.Equal, right))
        );

        var comparison = comparisonChar.LeftAssociative(
            (notLike, (left, right) => new BinaryExpression(left, BinaryOperator.NotLike, right)),
            (LIKE, (left, right) => new BinaryExpression(left, BinaryOperator.Like, right))
        );

        var bitwise = comparison.LeftAssociative(
            (Terms.Char('^'), (left, right) => new BinaryExpression(left, BinaryOperator.BitwiseXor, right)),
            (Terms.Char('&'), (left, right) => new BinaryExpression(left, BinaryOperator.BitwiseAnd, right)),
            (Terms.Char('|'), (left, right) => new BinaryExpression(left, BinaryOperator.BitwiseOr, right))
        );

        var andExpr = bitwise.LeftAssociative(
            (AND, (left, right) => new BinaryExpression(left, BinaryOperator.And, right))
        );

        var orExpr = andExpr.LeftAssociative(
            (OR, (left, right) => new BinaryExpression(left, BinaryOperator.Or, right))
        );

        var betweenExpr = andExpr.And(NOT.Optional()).AndSkip(BETWEEN).And(bitwise).AndSkip(AND).And(bitwise)
            .Then<Expression>(result =>
            {
                var (value, notKeyword, lower, upper) = result;
                return new BetweenExpression(value, lower, upper, notKeyword.HasValue);
            });

        var inExpr = andExpr.And(NOT.Optional()).AndSkip(IN).AndSkip(LPAREN).And(functionArgs).AndSkip(RPAREN)
            .Then<Expression>(result =>
            {
                var (value, notKeyword, values) = result;
                return new InExpression(value, values, notKeyword.HasValue);
            });

        expression.Parser = betweenExpr.Or(inExpr).Or(orExpr);

        var columnSourceId = identifier
            .Then<ColumnSource>(id => new ColumnSourceIdentifier(id))
            .Named("ColumnSourceIdentifier");

        var columnItemList = Separated(
            COMMA,
            columnItem.Or(STAR.Then(_ => StarColumnItem)).Named("ColumnItemOrStart"))
            .Named("ColumnItemList");
        var orderByList = Separated(COMMA, orderByItem);

        var orderByClause = ORDER.AndSkip(BY).And(orderByList)
            .Then(value => new OrderByClause(value.Item2));

        var partitionBy = PARTITION.AndSkip(BY).And(columnItemList)
            .Then(value => new PartitionByClause(value.Item2));

        var overClause = OVER.AndSkip(LPAREN)
            .And(partitionBy.Optional())
            .And(orderByClause.Optional())
            .AndSkip(RPAREN)
            .Then(result =>
            {
                var (_, partition, orderBy) = result;
                return new OverClause(
                    partition.OrSome(null),
                    orderBy.OrSome(null)
                );
            });

        var columnSourceFunc = functionCall.And(overClause.Optional())
            .Named("ColumnSourceFunction")
            .Then<ColumnSource>(result =>
            {
                var (function, over) = result;
                return new ColumnSourceFunction((FunctionCall)function, over.OrSome(null));
            });

        var columnSource = columnSourceFunc.Or(columnSourceId).Named("ColumnSource");

        var columnAlias = AS.Optional().SkipAnd(identifierNoKeywords);

        columnItem.Parser = columnSource.And(columnAlias.Optional())
            .Named("ColumnItem")
            .Then(result =>
            {
                var (source, alias) = result;
                return new ColumnItem(source, alias.OrSome(null));
            });

        var tableAlias = AS.Optional().SkipAnd(identifierNoKeywords);

        var tableSourceItem = identifier.And(tableAlias.Optional())
            .Then(result =>
            {
                var (id, alias) = result;
                return new TableSourceItem(id, alias.OrSome(null));
            });

        var unionStatementList = Deferred<IReadOnlyList<UnionStatement>>();

        var tableSourceSubQuery = LPAREN.SkipAnd(unionStatementList)
            .AndSkip(RPAREN)
            .AndSkip(AS)
            .And(simpleIdentifier)
            .Then<TableSource>(result =>
            {
                var (query, alias) = result;
                return new TableSourceSubQuery(query, alias.ToString());
            });

        var tableSourceItemAsTableSource = tableSourceItem.Then<TableSource>(table => table);
        var tableSource = tableSourceSubQuery.Or(tableSourceItemAsTableSource);
        var tableSourceList = Separated(COMMA, tableSource);

        var joinKind = INNER.Then(JoinKind.Inner)
            .Or(LEFT.Then(JoinKind.Left))
            .Or(RIGHT.Then(JoinKind.Right));

        var joinCondition = ON.SkipAnd(andExpr);
        var tableSourceItemList = Separated(COMMA, tableSourceItem);

        var joinStatement = joinKind.Else(JoinKind.None)
            .AndSkip(JOIN)
            .And(tableSourceItemList)
            .And(joinCondition)
            .Then(result =>
            {
                var (kind, tables, conditions) = result;
                return new JoinStatement(tables, conditions, kind);
            });

        var joins = ZeroOrMany(joinStatement);

        var fromClause = FROM.SkipAnd(tableSourceList).And(joins)
            .Then(result =>
            {
                var (tables, joinList) = result;
                return new FromClause(tables, joinList.Any() ? joinList : null);
            });

        var whereClause = WHERE.And(expression)
            .Then(value => new WhereClause(value.Item2));

        var columnSourceList = Separated(COMMA, columnSource);
        var groupByClause = GROUP.AndSkip(BY).And(columnSourceList)
            .Then(value => new GroupByClause(value.Item2));

        var havingClause = HAVING.And(expression)
            .Then(value => new HavingClause(value.Item2));

        var orderDirection = ASC.Then(OrderDirection.Asc)
            .Or(DESC.Then(OrderDirection.Desc));

        orderByItem.Parser =
            identifier.And(Between(LPAREN, functionArgs, RPAREN))
                .Then(result =>
                {
                    var (id, arguments) = result;
                    return new OrderByItem(id, arguments, OrderDirection.NotSpecified);
                })
                .Or(identifier.And(orderDirection.Optional())
                    .Then(result =>
                    {
                        var (id, direction) = result;
                        return new OrderByItem(id, null, direction.OrSome(OrderDirection.NotSpecified));
                    }));

        var limitClause = LIMIT.And(expression)
            .Then(value => new LimitClause(value.Item2));
        var offsetClause = OFFSET.And(expression)
            .Then(value => new OffsetClause(value.Item2));

        var selectRestriction = ALL.Then(SelectRestriction.All)
            .Or(DISTINCT.Then(SelectRestriction.Distinct));

        selectStatement.Parser = SELECT
            .SkipAnd(selectRestriction.Else(SelectRestriction.NotSpecified))
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
                    restriction,
                    from.OrSome(null),
                    where.OrSome(null),
                    groupBy.OrSome(null),
                    having.OrSome(null),
                    orderBy.OrSome(null),
                    limit.OrSome(null),
                    offset.OrSome(null)
                );
            });

        var columnNames = Separated(COMMA, simpleIdentifier);
        var cteColumnList = Between(LPAREN, columnNames, RPAREN);

        var cte = simpleIdentifier
            .And(cteColumnList.Optional())
            .AndSkip(AS)
            .And(Between(LPAREN, unionStatementList, RPAREN))
            .Then(result =>
            {
                var (name, columns, query) = result;
                return new CommonTableExpression(name, query, columns.OrSome(null));
            });

        var cteList = Separated(COMMA, cte);
        var withClause = WITH.And(cteList)
            .Then(value => new WithClause(value.Item2));

        var unionClause = UNION.And(ALL.Optional())
            .Then(value => new UnionClause(value.Item2.HasValue));

        var statement = withClause.Optional().And(selectStatement)
            .Then(result =>
            {
                var (with, select) = result;
                return new Statement(select, with.OrSome(null));
            });

        var unionStatement = statement.And(unionClause.Optional())
            .Then(result =>
            {
                var (stmt, union) = result;
                return new UnionStatement(stmt, union.OrSome(null));
            });

        unionStatementList.Parser = OneOrMany(unionStatement);

        var statementLine = unionStatementList.AndSkip(SEMICOLON.Optional())
            .Then(statements => new StatementLine(statements));

        var statementList = ZeroOrMany(statementLine)
            .Then(statements => new StatementList(statements))
            .AndSkip(Terms.WhiteSpace().Optional())
            .Eof();

        return statementList.WithComments(comments =>
        {
            comments
                .WithWhiteSpaceOrNewLine()
                .WithSingleLine("--")
                .WithMultiLine("/*", "*/");
        });
    }
}
