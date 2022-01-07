namespace Parlot.Fluent
{
    using System.Threading;

    public abstract partial class Parser<T>
    {
        private int _invocations = 0;
        private Parser<T> _compiledParser;

        public T Parse(string text)
        {
            var context = new ParseContext(new Scanner(text));

            return Parse(context);
        }

        public T Parse(ParseContext context)
        {
            var localResult = new ParseResult<T>();
            
            var success = CheckCompiled(context).Parse(context, ref localResult);

            if (success)
            {
                return localResult.Value;
            }

            return default;
        }

        private Parser<T> CheckCompiled(ParseContext context)
        {
            if (_compiledParser != null)
            {
                return _compiledParser;
            }

            if (context.CompilationThreshold > 0 && _invocations < context.CompilationThreshold)
            {
                if (Interlocked.Increment(ref _invocations) >= context.CompilationThreshold)
                {
                    lock (this)
                    {
                        if (_compiledParser == null)
                        {
                            return _compiledParser = this.Compile();
                        }
                    }                    
                }
            }

            return this;
        }

        public bool TryParse(string text, out T value)
        {
            return TryParse(text, out value, out _);
        }

        public bool TryParse(string text, out T value, out ParseError error)
        {
            return TryParse(new ParseContext(new Scanner(text)), out value, out error);
        }

        public bool TryParse(ParseContext context, out T value, out ParseError error)
        {
            error = null;

            try
            {
                var localResult = new ParseResult<T>();

                var success = CheckCompiled(context).Parse(context, ref localResult);

                if (success)
                {
                    value = localResult.Value;
                    return true;
                }
            }
            catch (ParseException e)
            {
                error = new ParseError
                {
                    Message = e.Message,
                    Position = e.Position
                };
            }

            value = default;
            return false;
        }
    }
}
