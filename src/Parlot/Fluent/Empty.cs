﻿using Parlot.Compilation;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    /// <summary>
    /// Doesn't parse anything and return the default value.
    /// </summary>
    public sealed class Empty<T, TParseContext> : Parser<T, TParseContext>, ICompilable<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly T _value;

        public Empty()
        {
            _value = default;
        }

        public Empty(T value)
        {
            _value = value;
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            result.Set(context.Scanner.Cursor.Offset, context.Scanner.Cursor.Offset, _value);

            return true;
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            var result = new CompilationResult();

            _ = context.DeclareSuccessVariable(result, true);
            _ = context.DeclareValueVariable(result, Expression.Constant(_value));

            return result;
        }
    }
}
