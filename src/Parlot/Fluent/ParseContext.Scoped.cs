﻿using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{

    public abstract class ParseContext<TChar, TParseContext> : ParseContextWithScanner<TChar>
    where TChar : IEquatable<TChar>, IConvertible
    where TParseContext : ParseContextWithScanner<TChar>
    {
        protected TParseContext parent;

        public ParseContext(TParseContext context)
        : this(context.Scanner)
        {
            OnEnterParser = context.OnEnterParser;
            parent = context;
        }

        public ParseContext(Scanner<TChar> scanner, bool useNewLines = false)
        : base(scanner)
        {
        }

        public abstract TParseContext Scope();
    }
}
