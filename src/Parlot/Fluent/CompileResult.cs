using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot
{
    public struct CompileResult
    {
        public static readonly CompileResult Empty = new CompileResult();

        public CompileResult(List<ParameterExpression> variables, List<Expression> body, ParameterExpression success, ParameterExpression value)
        {
            Variables = variables;
            Body = body;
            Success = success;
            Value = value;
        }

        // Variables used in the body
        public List<ParameterExpression> Variables;

        // Expression to execute
        public List<Expression> Body;

        // Expression containing the success of the parse phase once the Body is executed
        public ParameterExpression Success;

        // Expression containing the parsed value if the body was successful
        public ParameterExpression Value;
    }
}
