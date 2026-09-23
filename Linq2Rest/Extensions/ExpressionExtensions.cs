using System.Linq.Expressions;

namespace LinqConvertTools.Extensions
{
    public static class ExpressionExtensions
    {
        /// <summary>
        /// Rebinds an expression onto a parameter of type <typeparamref name="T"/>, resolving members by name on the new type.
        /// </summary>
        /// <remarks>
        /// For a lambda, its first parameter is rebound; otherwise every parameter not declared by a lambda inside the expression is rebound.
        /// Member paths keep their shape (<c>x.Address.City</c> stays nested), lambdas passed to generic methods such as
        /// <c>Any</c>/<c>All</c> are retyped to the new element type, and sub-trees that do not reference a rebound parameter
        /// (constants, captured closures) are left untouched. Mismatched operand types are bridged through implicit operators.
        /// </remarks>
        /// <typeparam name="T">The type to bind to.</typeparam>
        /// <param name="expression">The expression to rebind.</param>
        /// <param name="parameterExpression">The parameter to bind to, or null to create one named "x".</param>
        public static Expression CastParameter<T>(this Expression expression, ParameterExpression? parameterExpression)
        {
            return ParameterRebinder.Rebind(expression, typeof(T), parameterExpression);
        }

        /// <summary>
        /// Rebinds an expression onto a parameter of <paramref name="type"/> (see <see cref="CastParameter{T}"/>), replacing
        /// accesses to the member path <paramref name="memberName"/> with <paramref name="replacementMemberName"/>.
        /// </summary>
        /// <param name="type">The type to bind to.</param>
        /// <param name="memberName">The dot-separated member path, relative to the parameter, to replace.</param>
        /// <param name="replacementMemberName">The dot-separated member path to use instead, e.g. <c>EmailAddress.Value</c>.</param>
        /// <param name="expression">The expression to rebind.</param>
        /// <param name="parameterExpression">The parameter to bind to, or null to create one named "x".</param>
        public static Expression ReplaceMemberExpression(Type type, string memberName, string replacementMemberName, Expression expression, ParameterExpression? parameterExpression)
        {
            if (string.IsNullOrEmpty(memberName))
            {
                throw new ArgumentNullException(nameof(memberName));
            }

            if (string.IsNullOrEmpty(replacementMemberName))
            {
                throw new ArgumentNullException(nameof(replacementMemberName));
            }

            return ParameterRebinder.Rebind(expression, type, parameterExpression, memberName, replacementMemberName);
        }

        public static Expression ReplaceMemberExpression<T>(this Expression expression, string memberName, string replacementMemberName)
        {
            return ReplaceMemberExpression(typeof(T), memberName, replacementMemberName, expression, null);
        }
    }
}
