using System.Linq.Expressions;
using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public class CompilationContext
    {
        public CompilationContext(ParameterExpression parseContext)
        {
            ParseContext = parseContext;
        }

        public ParameterExpression ParseContext { get; }
        public Action<ParseContext, object> OnParse { get; set; }
        public int Counter { get; set; }
        public List<ParameterExpression> GlobalVariables = new List<ParameterExpression>();
        public List<Expression> GlobalExpressions = new List<Expression>();
    }
}
