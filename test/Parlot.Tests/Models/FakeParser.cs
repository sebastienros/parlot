using System;
using Parlot.Fluent;
using Parlot.Rewriting;

namespace Parlot.Tests.Models;

public partial class RewriteTests
{
    public sealed class FakeParser<T> : Parser<T>, ISeekable
    {
        public FakeParser()
        {
        }

        public T Result { get; set; }
        public bool CanSeek { get; set; }
        public char[] ExpectedChars { get; set; }
        public bool SkipWhitespace { get; set; }
        public bool Success { get; set; }
        public bool ThrowOnParse {get; set;}
        public Action<ParseContext> OnParse {get; set;}

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            if (ThrowOnParse)
            {
                throw new InvalidOperationException();
            }

            context.EnterParser(this);

            OnParse?.Invoke(context);

            if (Success)
            {
                result.Set(0, 0, Result);

                context.ExitParser(this);
                return true;
            }

            context.ExitParser(this);
            return false;
        }
    }
}
