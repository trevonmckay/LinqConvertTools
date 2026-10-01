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
            if (token == "1" || string.Equals(token, "true", StringComparison.OrdinalIgnoreCase))
            {
                return Expression.Constant(true);
            }

            if (token == "0" || string.Equals(token, "false", StringComparison.OrdinalIgnoreCase))
            {
                return Expression.Constant(false);
            }

            return Expression.Constant(null);
        }
    }
}