// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ByteArrayExpressionFactoryTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ByteArrayExpressionFactoryTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests.Parser.Readers
{
    using LinqConvertTools.Parser.Readers;
    using NUnit.Framework;
    using System;
    using System.Globalization;

    [TestFixture]
    public class ByteArrayExpressionFactoryTests
    {
        private const string Base64 = "TWFuIGlzIG/pc3Rpbmd1aXNoZWQsIG5vdCBvbmx5IGJ5IGhpcyByZWFzb24sIGJ1dCBieSB0aGlzIHNpbmd1bGFyIHBhc3Npb24gZnJvbSBvdGhlciBhbmltYWxzLCB3aGljaCBpcyBhIGx1c3Qgb2YgdGhlIG1pbmQsIHRoYXQgYnkgYSBwZXJzZXZlcmFuY2Ugb2YgZGVsaWdodCBpbiB0aGUgY29udGludWVkIGFuZCBpbmRlZmF0aWdhYmxlIGdlbmVyYXRpb24gb2Yga25vd2xlZGdlLCBleGNlZWRzIHRoZSBzaG9ydCB2ZWhlbWVuY2Ugb2YgYW55IGNhcm5hbCBwbGVhc3VyZS4=";
        private ByteArrayExpressionFactory? _factory;

        [SetUp]
        public void Setup()
        {
            _factory = new ByteArrayExpressionFactory();
        }

        [Test]
        public void WhenFilterIncludesBinaryParameterWithPrefixBinaryThenReturnedExpressionContainsByteArray()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var expression = _factory.Convert(string.Format(CultureInfo.InvariantCulture, "binary'{0}'", Base64));

            Assert.IsAssignableFrom<byte[]>(expression.Value);
        }

        [Test]
        public void WhenFilterIncludesBinaryParameterWithPrefixXThenReturnedExpressionContainsByteArray()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var expression = _factory.Convert(string.Format(CultureInfo.InvariantCulture, "X'{0}'", Base64));

            Assert.IsAssignableFrom<byte[]>(expression.Value);
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