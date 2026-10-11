// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IFilterExpressionFactory.cs" company="Reimers.dk">
//   Copyright � Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the public interface for a FilterExpressionFactory.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Parser
{
    using System;
    using System.Linq.Expressions;

    /// <summary>
    /// Defines the public interface for a FilterExpressionFactory.
    /// </summary>
    public interface IFilterExpressionFactory
    {
        /// <summary>
        /// Creates a filter expression from its string representation.
        /// </summary>
        /// <param name="filter">The string representation of the filter.</param>
        /// <param name="ignoreCase">When true the returned expression ensures string case is ignored.</param>
        /// <typeparam name="T">The <see cref="Type"/> of item to filter.</typeparam>
        /// <returns>An <see cref="Expression{TDelegate}"/> if the passed filter is valid, otherwise null.</returns>
        /// <remarks>
        /// Besides the OData grammar, <c>member not in (list)</c> is accepted as shorthand for
        /// <c>not (member in (list))</c>, including inside <c>any</c> and <c>all</c> lambdas.
        /// </remarks>
        Expression<Func<T, bool>> Create<T>(string filter, bool ignoreCase = false);

        /// <summary>
        /// Creates a filter expression from its string representation.
        /// </summary>
        /// <param name="filter">The string representation of the filter.</param>
        /// <param name="formatProvider">The <see cref="IFormatProvider"/> to use when reading the filter.</param>
        /// <param name="ignoreCase">When true the returned expression ensures string case is ignored.</param>
        /// <typeparam name="T">The <see cref="Type"/> of item to filter.</typeparam>
        /// <returns>An <see cref="Expression{TDelegate}"/> if the passed filter is valid, otherwise null.</returns>
        /// <remarks>
        /// Besides the OData grammar, <c>member not in (list)</c> is accepted as shorthand for
        /// <c>not (member in (list))</c>, including inside <c>any</c> and <c>all</c> lambdas.
        /// </remarks>
        Expression<Func<T, bool>> Create<T>(string filter, IFormatProvider formatProvider, bool ignoreCase = false);
    }
}