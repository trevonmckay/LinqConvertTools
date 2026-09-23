// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TimeSpanValueWriter.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the TimeSpanValueWriter type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Provider.Writers
{
    using System;
    using System.Globalization;
    using System.Xml;

    internal class TimeSpanValueWriter : ValueWriterBase<TimeSpan>
    {
        public override string Write(object value)
        {
            return string.Format(CultureInfo.InvariantCulture, "time'{0}'", XmlConvert.ToString((TimeSpan)value));
        }
    }
}