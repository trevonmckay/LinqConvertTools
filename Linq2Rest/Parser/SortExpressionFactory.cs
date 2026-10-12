// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SortExpressionFactory.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the SortExpressionFactory´.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;

    /// <summary>
    /// Defines the SortExpressionFactory´.
    /// </summary>
    public class SortExpressionFactory : ISortExpressionFactory
    {
        private readonly IMemberNameResolver _nameResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="SortExpressionFactory"/> class.
        /// </summary>
        /// <param name="nameResolver">The <see cref="IMemberNameResolver"/> for name resolution.</param>
        public SortExpressionFactory(IMemberNameResolver nameResolver)
        {
            _nameResolver = nameResolver;
        }

        /// <summary>
        /// Creates an enumeration of sort descriptions from its string representation.
        /// </summary>
        /// <param name="filter">The string representation of the sort descriptions.</param>
        /// <typeparam name="T">The <see cref="Type"/> of item to sort.</typeparam>
        /// <returns>An <see cref="IEnumerable{T}"/> if the passed sort descriptions are valid, otherwise null.</returns>
        public IEnumerable<SortDescription<T>> Create<T>(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return new SortDescription<T>[0];
            }

            var parameterExpression = Expression.Parameter(typeof(T), "x");

            var sortTokens = filter.Split(',');
            return from sortToken in sortTokens
                   select sortToken.Split(new[] { " " }, StringSplitOptions.RemoveEmptyEntries)
                       into sort
                   let propertyToken = sort.First()
                   where !string.IsNullOrWhiteSpace(propertyToken)
                   let property = GetPropertyExpression<T>(propertyToken, parameterExpression)
                   let direction = sort.ElementAtOrDefault(1) == "desc" ? SortDirection.Descending : SortDirection.Ascending
                   select new SortDescription<T>(property, direction);
        }

        private Expression GetPropertyExpression<T>(string propertyToken, ParameterExpression parameter)
        {
            var propertyExpression = _nameResolver.ResolveMemberPath(parameter, propertyToken.Split('/'))
                ?? throw new FormatException(propertyToken + " is not recognized as a valid property");

            var funcType = typeof(Func<,>).MakeGenericType(typeof(T), propertyExpression.Type);

            return Expression.Lambda(funcType, propertyExpression, parameter);
        }
    }
}