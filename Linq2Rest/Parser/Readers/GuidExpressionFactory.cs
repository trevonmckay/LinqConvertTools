// --------------------------------------------------------------------------------------------------------------------
// <copyright file="GuidExpressionFactory.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the GuidExpressionFactory type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Parser.Readers
{
    using System;
    using System.Linq.Expressions;
    using System.Text.RegularExpressions;

    internal class GuidExpressionFactory : ValueExpressionFactoryBase<Guid>
    {
        private static readonly Regex GuidRegex = new Regex(@"guid['\""]([a-f0-9\-]+)['\""]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // OData v4 literal: a bare 8-4-4-4-12 hex GUID (e.g. 0f000000-0000-7000-8000-000000000001) without the v3 guid'' wrapper.
        private static readonly Regex BareGuidRegex = new(@"^[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public override ConstantExpression Convert(string token)
        {
            var match = GuidRegex.Match(token);
            if (match.Success)
            {
                Guid guid;
                if (Guid.TryParse(match.Groups[1].Value, out guid))
                {
                    return Expression.Constant(guid);
                }
            }

            if (BareGuidRegex.IsMatch(token)
                && Guid.TryParseExact(token, "D", out Guid bareGuid))
            {
                return Expression.Constant(bareGuid);
            }

            throw new FormatException("Could not read " + token + " as Guid.");
        }
    }
}