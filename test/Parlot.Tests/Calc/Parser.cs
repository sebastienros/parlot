using System;

namespace Parlot.Tests.Calc
{

    // Recursive
    // https://craftinginterpreters.com/parsing-expressions.html#recursive-descent-parsing

    public class Parser
    {
        private Scanner<TokenTypes> _scanner;

        public Expression Parse(string text)
        {
            _scanner = new Scanner<TokenTypes>(text);

            return ParseExpression();
        }

        public bool TryParse(string text, out Expression expression, out ParseError error)
        {
            error = null;
            expression = null;

            try
            {
                expression = Parse(text);

                return true;
            }
            catch (ParseException e)
            {
                error = new ParseError
                {
                    Message = e.Message,
                    Position = _scanner.Cursor.Position
                };
            }

            return false;
        }

        /*
            expression     => term ;
            term           => factor ( ( "-" | "+" ) factor )* ;
            factor         => unary ( ( "/" | "*" ) unary )* ;
            unary          => ( "!" | "-" ) unary
                            | primary ;
            primary        => NUMBER
                            | "(" expression ")" ;
        */

        private Expression ParseExpression()
        {
            return ParseTerm();
        }

        private Expression ParseTerm()
        {
            var expression = ParseFactor();

            _scanner.SkipWhiteSpace();

            if (_scanner.ReadText("+", TokenTypes.Plus))
            {
                _scanner.SkipWhiteSpace();

                return new Addition(expression, ParseFactor());
            }

            if (_scanner.ReadText("-", TokenTypes.Plus))
            {
                _scanner.SkipWhiteSpace();

                return new Substraction(expression, ParseFactor());
            }

            return expression;
        }

        private Expression ParseFactor()
        {
            var expression = ParseUnaryExpression();

            _scanner.SkipWhiteSpace();

            if (_scanner.ReadText("*", TokenTypes.Times))
            {
                _scanner.SkipWhiteSpace();

                return new Multiplication(expression, ParseUnaryExpression());
            }

            if (_scanner.ReadText("/", TokenTypes.Divided))
            {
                _scanner.SkipWhiteSpace();

                return new Division(expression, ParseUnaryExpression());
            }

            return expression;
        }

        /*
         unary =    ( "!" | "-" ) unary
                    | primary ;
        */

        private Expression ParseUnaryExpression()
        {
            return ParsePrimaryExpression();
        }

        /*
          primary = NUMBER
                    | "(" expression ")" ;
        */

        private Expression ParsePrimaryExpression()
        {
            _scanner.SkipWhiteSpace();

            if (_scanner.ReadDecimal(TokenTypes.Number))
            {
                return new Number(decimal.Parse(_scanner.Token.Segment.Value));
            }

            if (_scanner.ReadText("(", TokenTypes.OpenBrace))
            {
                var expression = ParseExpression();

                if (!_scanner.ReadText(")", TokenTypes.CloseBrace))
                {
                    throw new ParseException("Expected ')'");
                }

                return expression;
            }

            throw new ParseException("Expected primary expression");
        }
    }
}
