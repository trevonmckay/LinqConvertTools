// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ByteExpressionFactoryTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ByteExpressionFactoryTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests.Parser.Readers
{
    using LinqConvertTools.Parser.Readers;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class ByteExpressionFactoryTests
    {
        private ByteExpressionFactory? _factory;

        [SetUp]
        public void Setup()
        {
            _factory = new ByteExpressionFactory();
        }

        [Test]
        public void WhenFilterIncludesByteParameterInHexFormatThenReturnedExpressionContainsByte()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var expression = _factory.Convert("f2");

            Assert.IsAssignableFrom<byte>(expression.Value);
        }

        [Test]
        public void WhenFilterIncludesByteParameterThenReturnedExpressionContainsByte()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var expression = _factory.Convert("12");

            Assert.IsAssignableFrom<byte>(expression.Value);
        }

        [Test]
        public void WhenFilterIsIncorrectFormatThenReturnsDefaultValue()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            const string Parameter = "blah";

            Assert.Throws<FormatException>(() => _factory.Convert(Parameter));
        }
    }
}