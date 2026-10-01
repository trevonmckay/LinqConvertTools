// --------------------------------------------------------------------------------------------------------------------
// <copyright file="BooleanExpressionFactory.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the BooleanExpressionFactory type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Parser.Readers
{
    using System;
    using System.Linq.Expressions;

    internal class BooleanExpressionFactory : ValueExpressionFactoryBase<bool>
    {
        public override ConstantExpression Convert(string token)
        {
            return TryParse(token, out bool value) ? Expression.Constant(value) : Expression.Constant(null);
        }

        /// <summary>
        /// Reads <c>1</c>, <c>0</c>, <c>true</c> or <c>false</c>, in any case, and nothing else.
        /// </summary>
        public static bool TryParse(string token, out bool value)
        {
            value = token == "1" || string.Equals(token, "true", StringComparison.OrdinalIgnoreCase);
            return value || token == "0" || string.Equals(token, "false", StringComparison.OrdinalIgnoreCase);
        }
    }
}