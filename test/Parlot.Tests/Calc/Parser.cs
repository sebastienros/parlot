namespace Parlot.Tests.Calc
{
    // Recursive descent parser
    // https://craftinginterpreters.com/parsing-expressions.html#recursive-descent-parsing

    /*
     * Grammar:
     * expression     => factor ( ( "-" | "+" ) factor )* ;
     * factor         => unary ( ( "/" | "*" ) unary )* ;
     * unary          => ( "-" ) unary
     *                 | primary ;
     * primary        => NUMBER
     *                  | "(" expression ")" ;
    */

    /// <summary>
    /// This verion of the Parser creates and intermediate AST.
    /// </summary>
    public class Parser : Parser<Expression>
    {
        private Scanner _scanner;

        public override Expression Parse(string text)
        {
            _scanner = new Scanner(text);

            return ParseExpression();
        }

        private Expression ParseExpression()
        {
            var expression = ParseFactor();

            while (true)
            {
                _scanner.SkipWhiteSpace();

                if (_scanner.ReadChar('+'))
                {
                    _scanner.SkipWhiteSpace();

                    expression = new Addition(expression, ParseFactor());
                }
                else if (_scanner.ReadChar('-'))
                {
                    _scanner.SkipWhiteSpace();

                    expression = new Substraction(expression, ParseFactor());
                }
                else
                {
                    break;
                }
            }

            return expression;
        }

        private Expression ParseFactor()
        {
            var expression = ParseUnaryExpression();

            while (true)
            {
                _scanner.SkipWhiteSpace();

                if (_scanner.ReadChar('*'))
                {
                    _scanner.SkipWhiteSpace();

                    expression = new Multiplication(expression, ParseUnaryExpression());
                }
                else if (_scanner.ReadChar('/'))
                {
                    _scanner.SkipWhiteSpace();

                    expression = new Division(expression, ParseUnaryExpression());
                }
                else
                {
                    break;
                }
            }

            return expression;
        }

        /*
         unary =    ( "-" ) unary
                    | primary ;
        */

        private Expression ParseUnaryExpression()
        {
            _scanner.SkipWhiteSpace();

            if (_scanner.ReadChar('-'))
            {
                var inner = ParseUnaryExpression();

                if (inner == null)
                {
                    throw new ParseException("Expected expression after '-'", _scanner.Cursor.Position);
                }

                return new NegateExpression(inner);
            }

            return ParsePrimaryExpression();
        }

        /*
          primary = NUMBER
                    | "(" expression ")" ;
        */

        private Expression ParsePrimaryExpression()
        {
            _scanner.SkipWhiteSpace();

            var number = new TokenResult();

            if (_scanner.ReadDecimal(number))
            {
                return new Number(decimal.Parse(number.Span));
            }

            if (_scanner.ReadChar('('))
            {
                var expression = ParseExpression();

                if (!_scanner.ReadChar(')'))
                {
                    throw new ParseException("Expected ')'", _scanner.Cursor.Position);
                }

                return expression;
            }

            throw new ParseException("Expected primary expression", _scanner.Cursor.Position);
        }
    }
}
