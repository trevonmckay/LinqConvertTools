// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DateTimeExpressionFactory.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the DateTimeExpressionFactory type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Parser.Readers
{
    using System;
    using System.Globalization;
    using System.Linq.Expressions;
    using System.Text.RegularExpressions;
    using System.Xml;

    internal class DateTimeExpressionFactory : ValueExpressionFactoryBase<DateTime>
    {
        private static readonly Regex DateTimeRegex = new Regex(@"datetime['\""](\d{4}\-\d{2}\-\d{2}(T\d{2}\:\d{2}\:\d{2}(.\d+)?)?(?<z>Z)?)['\""]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // OData v4 literal: a bare ISO-8601 date or date-time (e.g. 2012-01-01 or 2012-01-01T12:00:00Z), in the
        // same shape as the datetime'...' form so both share the conversion below.
        private static readonly Regex BareDateTimeRegex = new Regex(@"^(\d{4}\-\d{2}\-\d{2}(T\d{2}\:\d{2}\:\d{2}(\.\d+)?)?(?<z>Z)?)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public override ConstantExpression Convert(string token)
        {
            var match = DateTimeRegex.Match(token);
            if (!match.Success)
            {
                match = BareDateTimeRegex.Match(token);
            }

            if (match.Success)
            {
                var dateTime = match.Groups["z"].Success ?
                    XmlConvert.ToDateTime(match.Groups[1].Value, XmlDateTimeSerializationMode.Utc) :
                    DateTime.SpecifyKind(DateTime.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), DateTimeKind.Unspecified);
                return Expression.Constant(dateTime);
            }

            throw new FormatException("Could not read " + token + " as DateTime.");
        }
    }
}