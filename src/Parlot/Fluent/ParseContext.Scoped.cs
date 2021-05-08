using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public abstract class ParseContext<T, TParseContext> : ParseContextWithScanner<Scanner<T>, T>
    where T : IEquatable<T>, IConvertible
    where TParseContext : ParseContextWithScanner<Scanner<T>, T>
    {
        protected TParseContext parent;

        public ParseContext(TParseContext context)
        : this(context.Scanner)
        {
            OnEnterParser = context.OnEnterParser;
            parent = context;
        }

        public ParseContext(Scanner<T> scanner, bool useNewLines = false)
        : base(scanner)
        {
        }

        public abstract TParseContext Scope();
    }
}
