namespace Parlot.Fluent
{
    using System.Threading;

    public abstract partial class Parser<T>
    {
        private int _invocations = 0;
        private volatile Parser<T>? _compiledParser;

        public T? Parse(string text)
        {
            var context = new ParseContext(new Scanner(text));

            return Parse(context);
        }

        public T? Parse(ParseContext context)
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
            if (_compiledParser != null || context.CompilationThreshold == 0) 
            { 
                return _compiledParser ?? this; 
            }

            // Only the thread that reaches CompilationThreshold compiles the parser.
            // Any other concurrent call here will return 'this'. This prevents multiple compilations of 
            // the same parser, and a lock.

            if (Interlocked.Increment(ref _invocations) == context.CompilationThreshold)
            {
                return _compiledParser = this.Compile();
            }

            return this;
        }

        public bool TryParse(string text, out T? value) => TryParse(text, out value, out _);

        public bool TryParse(string text, out T value, out ParseError? error) => 
            TryParse(new ParseContext(new Scanner(text)), out value, out error);

        public bool TryParse(ParseContext context, out T value, out ParseError? error)
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

            value = default!;
            return false;
        }
    }
}
