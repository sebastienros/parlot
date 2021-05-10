using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public partial class ParseContext
    {
        public class Untyped : ScopeParseContext<Untyped>
        {
            IDictionary<string, object> scope;

            private bool HasValue(string name)
            {
                return scope != null && scope.TryGetValue(name, out _) || parent != null && parent.HasValue(name);
            }

            public void Set(string name, object value)
            {
                if (parent != null && parent.HasValue(name))
                    parent.Set(name, value);
                else
                {
                    if (scope == null)
                        scope = new Dictionary<string, object>();
                    scope[name] = value;
                }
            }

            public T Get<T>(string name)
            {
                if (scope != null && scope.TryGetValue(name, out var result))
                    return (T)result;
                if (parent == null)
                    return default(T);
                return parent.Get<T>(name);
            }

            protected Untyped(Untyped context)
            : base(context)
            {
            }

            public Untyped(Scanner scanner, bool useNewLines = false)
            : base(scanner, useNewLines)
            {
            }

            public override Untyped Scope()
            {
                return new Untyped(this);
            }

            public static Untyped Scan(Scanner scanner, bool useNewLines = false)
            {
                return new Untyped(scanner, useNewLines);
            }
        }
    }
}
