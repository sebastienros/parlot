using System;
using System.Collections.Generic;
using System.Text;

namespace Parlot
{
    public class ParseError
    {
        public string Message { get; set; }
        public TextPosition Position { get; set; }
    }
}
