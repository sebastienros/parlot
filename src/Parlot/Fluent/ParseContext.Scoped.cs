using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public abstract class ParseContext<TParseContext> : ParseContext
    where TParseContext : ParseContext
    {
        protected TParseContext parent;

        public ParseContext(TParseContext context)
        : this(context.Scanner, context.UseNewLines)
        {
            OnEnterParser = context.OnEnterParser;
            WhiteSpaceParser = context.WhiteSpaceParser;
            parent = context;
        }

        public ParseContext(Scanner scanner, bool useNewLines = false) : base(scanner, useNewLines)
        {
        }

        public abstract TParseContext Scope();
    }
}
