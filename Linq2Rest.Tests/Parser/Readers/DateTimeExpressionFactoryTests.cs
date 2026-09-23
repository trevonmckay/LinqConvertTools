// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DateTimeExpressionFactoryTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the DateTimeExpressionFactoryTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests.Parser.Readers
{
    using LinqConvertTools.Parser.Readers;
    using NUnit.Framework;
    using System;
    using System.Globalization;

    [TestFixture]
    public class DateTimeExpressionFactoryTests
    {
        private DateTimeExpressionFactory? _factory;
        private DateTime _dateTime;

        [SetUp]
        public void Setup()
        {
            _factory = new DateTimeExpressionFactory();
            _dateTime = new DateTime(2012, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);
        }

        [Test]
        public void WhenFilterIncludesDateTimeParameterInDoubleQuotesThenReturnedExpressionContainsDateTime()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var parameter = string.Format("datetime\"{0}\"", _dateTime.ToString("yyyy-MM-ddThh:mm:ss"));

            var expression = _factory.Convert(parameter);

            Assert.AreEqual(_dateTime, expression.Value);
        }

        [Test]
        public void WhenFilterIncludesDateTimeParameterThenReturnedExpressionContainsDateTime()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var parameter = string.Format("datetime'{0}'", _dateTime.ToString("yyyy-MM-ddThh:mm:ss"));

            var expression = _factory.Convert(parameter);

            Assert.AreEqual(_dateTime, expression.Value);
        }

        [Test]
        public void WhenFilterIncludesDateTimeParameterWithMillisecondsThenReturnedExpressionContainsDateTime()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            _dateTime = new DateTime(2012, 1, 1, 12, 0, 0, 11, DateTimeKind.Utc);
            var parameter = string.Format("datetime'{0}'", _dateTime.ToString("o"));

            var expression = _factory.Convert(parameter);

            Assert.AreEqual(_dateTime, expression.Value);
        }

        [Test]
        public void WhenFilterIncludesDateTimeParameterWithZuluInDoubleQuotesThenReturnedExpressionContainsUtcDateTime()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var utcTime = _dateTime.ToUniversalTime();
            var parameter = string.Format("datetime\"{0}\"", utcTime.ToString("yyyy-MM-ddTHH:mm:ssZ"));

            var expression = _factory.Convert(parameter);

            Assert.AreEqual(utcTime, expression.Value);
        }

        [Test]
        public void WhenFilterIncludesDateTimeParameterWithZuluThenReturnedExpressionContainsUtcDateTime()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var utcTime = _dateTime.ToUniversalTime();
            var parameter = string.Format("datetime'{0}'", utcTime.ToString("yyyy-MM-ddTHH:mm:ssZ"));

            var expression = _factory.Convert(parameter);

            Assert.AreEqual(utcTime, expression.Value);
        }

        [Test]
        public void WhenFilterIncludesBareV4DateTimeThenReturnedExpressionContainsDateTime()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var parameter = _dateTime.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            var expression = _factory.Convert(parameter);

            Assert.AreEqual(_dateTime, expression.Value);
        }

        [Test]
        public void WhenFilterIncludesBareV4DateTimeWithZuluThenReturnedExpressionContainsUtcDateTime()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var utcTime = _dateTime.ToUniversalTime();
            var parameter = utcTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

            var expression = _factory.Convert(parameter);

            Assert.AreEqual(utcTime, expression.Value);
            Assert.AreEqual(DateTimeKind.Utc, ((DateTime)expression.Value!).Kind);
        }

        [Test]
        public void WhenFilterIncludesBareV4DateThenReturnedExpressionContainsDateTime()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var expression = _factory.Convert("2012-01-01");

            Assert.AreEqual(new DateTime(2012, 1, 1), expression.Value);
        }

        [Test]
        public void WhenFilterIsIncorrectFormatThenThrows()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            const string Parameter = "blah";

            Assert.Throws<FormatException>(() => _factory.Convert(Parameter));
        }
    }
}