using Parlot.Fluent;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Compilation
{
    /// <summary>
    /// Reprensents the context of a compilation phase, coordinating all the parsers involved.
    /// </summary>
    public class CompilationContext
    {
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
        public int Counter { get; set; }

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
        /// This is used for debug only, in order to inpect the source generated for these intermediate parsers.
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
    }
}
