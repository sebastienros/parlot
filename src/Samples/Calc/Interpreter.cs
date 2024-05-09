using System.Globalization;

namespace Parlot.Tests.Calc
{
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
    /// This version of the Interpreter evaluates the value while it parses the expression.
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
            var value = ParseFactor();

            _scanner.SkipWhiteSpace();

            while (true)
            {
                if (_scanner.ReadChar('+'))
                {
                    _scanner.SkipWhiteSpace();

                    value += ParseFactor();
                }
                else if (_scanner.ReadChar('-'))
                {
                    _scanner.SkipWhiteSpace();

                    value -= ParseFactor();
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
                if (_scanner.ReadChar('*'))
                {
                    _scanner.SkipWhiteSpace();

                    value *= ParseUnaryExpression();
                }
                else if (_scanner.ReadChar('/'))
                {
                    _scanner.SkipWhiteSpace();

                    value /= ParseUnaryExpression();
                }
                else
                {
                    break;
                }
            }

            return value;
        }

        /*
         unary =    ( "-" ) unary
                    | primary ;
        */

        private decimal ParseUnaryExpression()
        {
            _scanner.SkipWhiteSpace();

            if (_scanner.ReadChar('-'))
            {
                return -1 * ParseUnaryExpression();
            }

            return ParsePrimaryExpression();
        }

        /*
          primary = NUMBER
                    | "(" expression ")" ;
        */

        private decimal ParsePrimaryExpression()
        {
            _scanner.SkipWhiteSpace();

            if (_scanner.ReadDecimal(out var number))
            {
#if NETCOREAPP2_1
                return decimal.Parse(number.GetText(), provider: CultureInfo.InvariantCulture);
#else
                return decimal.Parse(number.Span, provider: CultureInfo.InvariantCulture);
#endif
            }

            if (_scanner.ReadChar('('))
            {
                var value = ParseExpression();

                if (!_scanner.ReadChar(')'))
                {
                    throw new ParseException("Expected ')'", _scanner.Cursor.Position);
                }

                return value;
            }

            throw new ParseException("Expected primary expression", _scanner.Cursor.Position);
        }
    }
}
