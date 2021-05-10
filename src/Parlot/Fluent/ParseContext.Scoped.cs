using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public abstract class ScopeParseContext<TParseContext> : ParseContext
    where TParseContext : ParseContext
    {
        protected TParseContext parent;

        public ScopeParseContext(TParseContext context)
        : this(context.Scanner, context.UseNewLines)
        {
            OnEnterParser = context.OnEnterParser;
            WhiteSpaceParser = context.WhiteSpaceParser;
            parent = context;
        }

        public ScopeParseContext(Scanner scanner, bool useNewLines = false) : base(scanner, useNewLines)
        {
        }

        public abstract TParseContext Scope();
    }
}
