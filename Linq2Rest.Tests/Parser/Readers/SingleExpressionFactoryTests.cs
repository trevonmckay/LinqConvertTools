// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SingleExpressionFactoryTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the SingleExpressionFactoryTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests.Parser.Readers
{
    using LinqConvertTools.Parser.Readers;
    using NUnit.Framework;
    using System;

    [TestFixture]
    public class SingleExpressionFactoryTests
    {
        private SingleExpressionFactory? _factory;

        [SetUp]
        public void Setup()
        {
            _factory = new SingleExpressionFactory();
        }

        [Test]
        public void WhenFilterIncludesSingleParameterThenReturnedExpressionContainsSingle()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var expression = _factory.Convert("1.23");

            Assert.IsAssignableFrom<float>(expression.Value);
        }

        [Test]
        public void WhenFilterIncludesSingleParameterWithTrailingLowerCaseMThenReturnedExpressionContainsSingle()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var expression = _factory.Convert("1.23f");

            Assert.IsAssignableFrom<float>(expression.Value);
        }

        [Test]
        public void WhenFilterIncludesSingleParameterWithTrailingUpperCaseMThenReturnedExpressionContainsSingle()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var expression = _factory.Convert("1.23F");

            Assert.IsAssignableFrom<float>(expression.Value);
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