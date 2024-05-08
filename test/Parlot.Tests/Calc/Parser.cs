using System.Globalization;

namespace Parlot.Tests.Calc
{
    using Domain;

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
    /// This version of the Parser creates and intermediate AST.
    /// </summary>
    public class Parser
    {
        private Scanner _scanner;

        public Expression<decimal> Parse(string text)
        {
            _scanner = new Scanner(text);

            return ParseExpression();
        }

        private Expression<decimal> ParseExpression()
        {
            var expression = ParseFactor();

            while (true)
            {
                _scanner.SkipWhiteSpace();

                if (_scanner.ReadChar('+'))
                {
                    _scanner.SkipWhiteSpace();

                    expression = new Addition<decimal>(expression, ParseFactor());
                }
                else if (_scanner.ReadChar('-'))
                {
                    _scanner.SkipWhiteSpace();

                    expression = new Subtraction<decimal>(expression, ParseFactor());
                }
                else
                {
                    break;
                }
            }

            return expression;
        }

        private Expression<decimal> ParseFactor()
        {
            var expression = ParseUnaryExpression();

            while (true)
            {
                _scanner.SkipWhiteSpace();

                if (_scanner.ReadChar('*'))
                {
                    _scanner.SkipWhiteSpace();

                    expression = new Multiplication<decimal>(expression, ParseUnaryExpression());
                }
                else if (_scanner.ReadChar('/'))
                {
                    _scanner.SkipWhiteSpace();

                    expression = new Division<decimal>(expression, ParseUnaryExpression());
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

        private Expression<decimal> ParseUnaryExpression()
        {
            _scanner.SkipWhiteSpace();

            if (_scanner.ReadChar('-'))
            {
                var inner = ParseUnaryExpression();

                if (inner == null)
                {
                    throw new ParseException("Expected expression after '-'", _scanner.Cursor.Position);
                }

                return new NegateExpression<decimal>(inner);
            }

            return ParsePrimaryExpression();
        }

        /*
          primary = NUMBER
                    | "(" expression ")" ;
        */

        private Expression<decimal> ParsePrimaryExpression()
        {
            _scanner.SkipWhiteSpace();

            if (_scanner.ReadDecimal(out var number))
            {
                return new Decimal(decimal.Parse(number.Span, provider: CultureInfo.InvariantCulture));
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
