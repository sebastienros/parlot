using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Compilation
{
    /// <summary>
    /// Every parser that is compiled returns an instance of <see cref="CompilationResult"/> which encapsulates the statements to execute in order
    /// to parse the expected input.
    /// The convention is that these statements are returned in the <see cref="Body"/> property, and any variable that needs to be declared in the block
    /// that the <see cref="Body"/> is used in are set in the <see cref="Variables"/> list.
    /// The <see cref="Success"/> property represents the variable that contains the success of the statements once executed, and if <code>true</code> then 
    /// the <see cref="Value"/> property contains the result.
    /// </summary>
    public class CompilationResult
    {
        internal CompilationResult()
        {
        }

        /// <summary>
        /// Gets the list of <see cref="ParameterExpression"/> representing the variables used by the compiled result.
        /// </summary>
        public List<ParameterExpression> Variables { get; } = [];

        /// <summary>
        /// Gets the list of <see cref="Expression"/> representing the body of the compiled results.
        /// </summary>
        public List<Expression> Body { get; } = [];

        /// <summary>
        /// Gets or sets the <see cref="ParameterExpression"/> of the <see cref="bool"/> variable representing the success of the parsing statements.
        /// </summary>
        public required ParameterExpression Success { get; set; }

        /// <summary>
        /// Gets or sets the <see cref="ParameterExpression"/> of the <see cref="bool"/> variable representing the value of the parsing statements.
        /// </summary>
        public required ParameterExpression Value { get; set; }

        public ParameterExpression DeclareVariable<T>(string name, Expression? defaultValue = null)
        {
            return DeclareVariable(name, typeof(T), defaultValue);
        }

        public ParameterExpression DeclareVariable(string name, Type type, Expression? defaultValue = null)
        {
            var variable = Expression.Variable(type, name);
            
            Variables.Add(variable);
            Body.Add(Expression.Assign(variable, defaultValue ?? Expression.Default(type)));

            return variable;
        }
    }
}
