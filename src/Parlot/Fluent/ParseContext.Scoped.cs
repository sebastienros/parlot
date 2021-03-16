using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public partial class ParseContext 
    {
        IDictionary<string, object> scope ;
        private ParseContext parent;

        private bool HasValue(string name)
        {
            return scope!=null && scope.TryGetValue(name, out _) || parent != null && parent.HasValue(name);
        }

        public void Set(string name, object value)
        {
            if (parent != null && parent.HasValue(name))
                parent.Set(name, value);
            else 
            {
                if(scope == null)
                    scope = new Dictionary<string,object>();
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

        public ParseContext(ParseContext context)
        {

            Scanner=context.Scanner;
            UseNewLines=context.UseNewLines;
            OnEnterParser=context.OnEnterParser;
            WhiteSpaceParser=context.WhiteSpaceParser;
            parent = context ;
        }
    }
}
