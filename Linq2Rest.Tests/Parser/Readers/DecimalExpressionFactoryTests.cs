// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DecimalExpressionFactoryTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the DecimalExpressionFactoryTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests.Parser.Readers
{
    using LinqConvertTools.Parser.Readers;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class DecimalExpressionFactoryTests
    {
        private DecimalExpressionFactory? _factory;

        [SetUp]
        public void Setup()
        {
            _factory = new DecimalExpressionFactory();
        }

        [Test]
        public void WhenFilterIncludesDecimalParameterThenReturnedExpressionContainsDecimal()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var expression = _factory.Convert("1.23");

            Assert.IsAssignableFrom<decimal>(expression.Value);
        }

        [Test]
        public void WhenFilterIncludesDecimalParameterWithTrailingLowerCaseMThenReturnedExpressionContainsDecimal()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var expression = _factory.Convert("1.23m");

            Assert.IsAssignableFrom<decimal>(expression.Value);
        }

        [Test]
        public void WhenFilterIncludesDecimalParameterWithTrailingUpperCaseMThenReturnedExpressionContainsDecimal()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var expression = _factory.Convert("1.23M");

            Assert.IsAssignableFrom<decimal>(expression.Value);
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