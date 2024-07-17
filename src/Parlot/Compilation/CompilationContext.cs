using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Compilation
{
    /// <summary>
    /// Represents the context of a compilation phase, coordinating all the parsers involved.
    /// </summary>
    public class CompilationContext
    {
        private int _number = 0;

        public CompilationContext()
        {
        }

        /// <summary>
        /// Gets the expression containing the the <see cref="ParseContext"/> instance for the parser.
        /// </summary>
        public ParameterExpression ParseContext { get; } = Expression.Parameter(typeof(ParseContext));

        /// <summary>
        /// Gets or sets a counter used to generate unique variable names.
        /// </summary>
        public int NextNumber => _number++;

        /// <summary>
        /// Gets the list of global variables to add the the final list of statements.
        /// </summary>
        public List<ParameterExpression> GlobalVariables { get; } = new();

        /// <summary>
        /// Gets the list of global expressions to add the the final list of statements.
        /// </summary>
        public List<Expression> GlobalExpressions { get; } = new();
        
        /// <summary>
        /// Gets the list of shared lambda expressions representing intermediate parsers.
        /// </summary>
        /// <remarks>
        /// This is used for debug only, in order to inspect the source generated for these intermediate parsers.
        /// </remarks>
        public List<Expression> Lambdas { get; } = new();

        /// <summary>
        /// Gets or sets whether the current compilation phase should ignore the results of the parsers.
        /// </summary>
        /// <remarks>
        /// When set to false, the compiled statements don't need to record and define the <see cref="CompilationResult.Value"/> property.
        /// This is done to optimize compiled parser that are usually used for pattern matching only.
        /// </remarks>
        public bool DiscardResult { get; set; } = false;

        /// <summary>
        /// Creates a <see cref="CompilationResult"/> instance with a <see cref="CompilationResult.Value"/> and <see cref="CompilationResult.Success"/>
        /// variables.
        /// </summary>
        /// <typeparam name="TValue">The type of the value returned by the parser instance.</typeparam>
        /// <param name="defaultSuccess">The default value of the <see cref="CompilationResult.Success"/> variable.</param>
        /// <param name="defaultValue">The default value of the <see cref="CompilationResult.Value"/> variable.</param>
        /// <returns></returns>
        public CompilationResult CreateCompilationResult<TValue>(bool defaultSuccess = false, Expression? defaultValue = null) => 
            CreateCompilationResult(typeof(TValue), defaultSuccess, defaultValue);

        /// <summary>
        /// Creates a <see cref="CompilationResult"/> instance with a <see cref="CompilationResult.Value"/> and <see cref="CompilationResult.Success"/>
        /// variables.
        /// </summary>
        /// <param name="valueType">The type of the value returned by the parser instance.</param>
        /// <param name="defaultSuccess">The default value of the <see cref="CompilationResult.Success"/> variable.</param>
        /// <param name="defaultValue">The default value of the <see cref="CompilationResult.Value"/> variable.</param>
        /// <returns></returns>
        public CompilationResult CreateCompilationResult(Type valueType, bool defaultSuccess = false, Expression? defaultValue = null)
        {
            var successVariable = Expression.Variable(typeof(bool), $"success{this.NextNumber}");
            var valueVariable = Expression.Variable(valueType, $"value{this.NextNumber}");

            var result = new CompilationResult { Success = successVariable, Value = valueVariable };

            result.Variables.Add(successVariable);
            result.Variables.Add(valueVariable);

            result.Body.Add(Expression.Assign(successVariable, Expression.Constant(defaultSuccess, typeof(bool))));
            result.Body.Add(Expression.Assign(valueVariable, defaultValue ?? Expression.Default(valueType)));

            return result;
        }

    }
}
