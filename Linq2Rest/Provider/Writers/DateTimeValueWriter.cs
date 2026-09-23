// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DateTimeValueWriter.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the DateTimeValueWriter type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Provider.Writers
{
    using System;
    using System.Globalization;
    using System.Xml;

    internal class DateTimeValueWriter : ValueWriterBase<DateTime>
    {
        public override string Write(object value)
        {
            var dateTimeValue = (DateTime)value;

#if !NETFX_CORE
            return string.Format(CultureInfo.InvariantCulture, "datetime'{0}'", XmlConvert.ToString(dateTimeValue, XmlDateTimeSerializationMode.Utc));
#else
			return string.Format("datetime'{0}'", XmlConvert.ToString(dateTimeValue.ToUniversalTime()));
#endif
        }
    }
}
