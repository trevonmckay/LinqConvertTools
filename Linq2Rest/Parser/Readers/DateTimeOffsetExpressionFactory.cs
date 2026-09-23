// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DateTimeOffsetExpressionFactory.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the DateTimeOffsetExpressionFactory type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Parser.Readers
{
    using System;
    using System.Globalization;
    using System.Linq.Expressions;
    using System.Text.RegularExpressions;
    using System.Xml;

    internal class DateTimeOffsetExpressionFactory : ValueExpressionFactoryBase<DateTimeOffset>
    {
        private static readonly Regex DateTimeOffsetRegex = new(@"datetimeoffset['\""]([TZ:\d.+-]+)['\""]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // OData v4 literal: a bare ISO-8601 date-time with a required offset (e.g. 2012-05-06T18:10:00Z or
        // 2012-05-06T18:10:00.123+02:00). A date-only value is not a DateTimeOffset literal in v4.
        private static readonly Regex BareDateTimeOffsetRegex = new(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public override ConstantExpression Convert(string token)
        {
            var match = DateTimeOffsetRegex.Match(token);
            string dateTimeOffsetString = match.Groups[1].Value;
            if (match.Success && DateTimeOffset.TryParse(dateTimeOffsetString, out DateTimeOffset dateTimeOffset))
            {
                return Expression.Constant(dateTimeOffset);
            }

            if (BareDateTimeOffsetRegex.IsMatch(token)
                && DateTimeOffset.TryParse(token, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset bareDateTimeOffset))
            {
                return Expression.Constant(bareDateTimeOffset);
            }

            throw new FormatException("Could not read " + token + " as DateTimeOffset.");
        }
    }
}