namespace Parlot.Tests.Calc
{

    // Recursive descent parser
    // https://craftinginterpreters.com/parsing-expressions.html#recursive-descent-parsing

    /*
     * Grammar:
     * expression     => term ;
     * term           => factor ( ( "-" | "+" ) factor )* ;
     * factor         => unary ( ( "/" | "*" ) unary )* ;
     * unary          => ( "!" | "-" ) unary
     *                 | primary ;
     * primary        => NUMBER
     *                  | "(" expression ")" ;
    */

    /// <summary>
    /// This verion of the Interpreter evaluates the value while it parses the expression.
    /// </summary>
    public class Interpreter
    {
        private Scanner _scanner;

        public decimal Evaluate(string text)
        {
            _scanner = new Scanner(text);

            return ParseExpression();
        }

        private decimal ParseExpression()
        {
            return ParseTerm();
        }

        private decimal ParseTerm()
        {
            var value = ParseFactor();

            _scanner.SkipWhiteSpace();

            while (true)
            {
                if (_scanner.ReadText("+"))
                {
                    _scanner.SkipWhiteSpace();

                    value = value + ParseFactor();
                }
                else if (_scanner.ReadText("-"))
                {
                    _scanner.SkipWhiteSpace();

                    value = value - ParseFactor();
                }
                else
                {
                    break;
                }
            }

            return value;
        }

        private decimal ParseFactor()
        {
            var value = ParseUnaryExpression();

            _scanner.SkipWhiteSpace();

            while (true)
            {
                if (_scanner.ReadText("*"))
                {
                    _scanner.SkipWhiteSpace();

                    value = value * ParseUnaryExpression();
                }
                else if (_scanner.ReadText("/"))
                {
                    _scanner.SkipWhiteSpace();

                    value = value / ParseUnaryExpression();
                }
                else
                {
                    break;
                }
            }

            return value;
        }

        /*
         unary =    ( "!" | "-" ) unary
                    | primary ;
        */

        private decimal ParseUnaryExpression()
        {
            return ParsePrimaryExpression();
        }

        /*
          primary = NUMBER
                    | "(" expression ")" ;
        */

        private decimal ParsePrimaryExpression()
        {
            _scanner.SkipWhiteSpace();

            if (_scanner.ReadDecimal("number"))
            {
                return decimal.Parse(_scanner.Token.Span);
            }

            if (_scanner.ReadText("("))
            {
                var value = ParseExpression();

                if (!_scanner.ReadText(")"))
                {
                    throw new ParseException("Expected ')'");
                }

                return value;
            }

            throw new ParseException("Expected primary expression");
        }
    }
}
