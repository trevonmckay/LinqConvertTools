// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DateTimeOffsetExpressionFactoryTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the DateTimeOffsetExpressionFactoryTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests.Parser.Readers
{
    using LinqConvertTools.Parser.Readers;
    using NUnit.Framework;
    using System;
    using System.Xml;

    [TestFixture]
    public class DateTimeOffsetExpressionFactoryTests
    {
        private DateTimeOffsetExpressionFactory _factory;

        [SetUp]
        public void Setup()
        {
            _factory = new DateTimeOffsetExpressionFactory();
        }

        [Test]
        public void WhenFilterIncludesDateTimeOffsetParameterInDoubleQuotesThenReturnedExpressionContainsDateTimeOffset()
        {
            var dateTimeOffset = new DateTimeOffset(2012, 5, 6, 18, 10, 0, 100, TimeSpan.FromHours(2));
            var parameter = string.Format("datetimeoffset\"{0}\"", XmlConvert.ToString(dateTimeOffset));

            var expression = _factory.Convert(parameter);

            Assert.IsAssignableFrom<DateTimeOffset>(expression.Value);
        }

        [Test]
        public void WhenFilterIncludesDateTimeOffsetParameterThenReturnedExpressionContainsDateTimeOffset()
        {
            var dateTimeOffset = new DateTimeOffset(2012, 5, 6, 18, 10, 0, 100, TimeSpan.FromHours(2));
            var parameter = string.Format("datetimeoffset'{0}'", XmlConvert.ToString(dateTimeOffset));

            var expression = _factory.Convert(parameter);

            Assert.IsAssignableFrom<DateTimeOffset>(expression.Value);
        }

        [TestCase("2012-05-06T18:10:00Z", 0)]
        [TestCase("2012-05-06T18:10:00.100+02:00", 2)]
        [TestCase("2012-05-06T18:10-05:00", -5)]
        public void WhenFilterIncludesBareV4DateTimeOffsetThenReturnedExpressionContainsDateTimeOffset(string parameter, int offsetHours)
        {
            var expression = _factory.Convert(parameter);

            Assert.IsAssignableFrom<DateTimeOffset>(expression.Value);
            Assert.AreEqual(TimeSpan.FromHours(offsetHours), ((DateTimeOffset)expression.Value!).Offset);
        }

        [TestCase("2012-05-06")]
        [TestCase("2012-05-06T18:10:00")]
        public void WhenBareDateTimeOffsetHasNoOffsetThenThrows(string parameter)
        {
            Assert.Throws<FormatException>(() => _factory.Convert(parameter));
        }

        [Test]
        public void WhenFilterIsIncorrectFormatThenThrows()
        {
            const string Parameter = "blah";

            Assert.Throws<FormatException>(() => _factory.Convert(Parameter));
        }
    }
}