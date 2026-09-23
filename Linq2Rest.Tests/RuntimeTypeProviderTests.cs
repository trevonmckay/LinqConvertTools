// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RuntimeTypeProviderTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the RuntimeTypeProviderTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests
{
    using NUnit.Framework;
    using System;
    using System.Reflection;

    [TestFixture]
    public class RuntimeTypeProviderTests
    {
        private RuntimeTypeProvider? _typeProvider;

        [SetUp]
        public void Setup()
        {
            _typeProvider = new RuntimeTypeProvider(new MemberNameResolver());
        }

        [Test]
        public void WhenCreatingDynamicTypeThenTransfersCustomAttributesWithDefaultConstructor()
        {
            ArgumentNullException.ThrowIfNull(_typeProvider);

            var properties = new[] { typeof(FakeItem).GetProperty("DateValue")! };

            var dynamicType = _typeProvider.Get(typeof(FakeItem), properties);

            Assert.AreEqual(1, dynamicType.GetProperties().Length);
            Assert.NotNull(dynamicType.GetProperty("DateValue"));
        }

        [Test]
        public void WhenCreatingDynamicTypeWithNoPropertiesThenThrows()
        {
            ArgumentNullException.ThrowIfNull(_typeProvider);

            Assert.Throws<ArgumentOutOfRangeException>(() => _typeProvider.Get(typeof(FakeItem), new PropertyInfo[0]));
        }

        [Test]
        public void WhenCreatingDynamicTypeWithNullFiledsThenThrows()
        {
            ArgumentNullException.ThrowIfNull(_typeProvider);

            // Passes null deliberately to verify that Get throws ArgumentNullException.
#pragma warning disable CS8600, CS8604
            PropertyInfo[] propertyInfos = null;
            Assert.Throws<ArgumentNullException>(() => _typeProvider.Get(typeof(FakeItem), propertyInfos));
#pragma warning restore CS8600, CS8604
        }

        [Test]
        public void WhenCreatingDynamicTypeWithOnePropertyInfoThenCreatesTypeWithOneProperty()
        {
            ArgumentNullException.ThrowIfNull(_typeProvider);

            var properties = new[] { typeof(FakeItem).GetProperty("ChoiceValue")! };

            var dynamicType = _typeProvider.Get(typeof(FakeItem), properties);

            var dataMemberAttribute = dynamicType
                .GetProperty("Choice")!
                .GetCustomAttributes(false);

            Assert.IsNotEmpty(dataMemberAttribute);
        }

        [Test]
        public void WhenCreatingDynamicTypeWithOnePropertyInfoThenCreatesTypeWithOnePropertyWhereTypeMatchesProperty()
        {
            ArgumentNullException.ThrowIfNull(_typeProvider);

            var properties = new[] { typeof(FakeItem).GetProperty("DateValue")! };

            var dynamicType = _typeProvider.Get(typeof(FakeItem), properties);
            var property = dynamicType.GetProperty("DateValue")!;

            Assert.AreEqual(typeof(DateTime), property.PropertyType);
        }

        [Test]
        public void WhenCreatingDynamicTypeWithOnePropertyInfoThenGettingValueReturnsSetValue()
        {
            ArgumentNullException.ThrowIfNull(_typeProvider);

            var expected = DateTime.UtcNow;
            var properties = new[] { typeof(FakeItem).GetProperty("DateValue")! };

            var dynamicType = _typeProvider.Get(typeof(FakeItem), properties);

            dynamic instance = Activator.CreateInstance(dynamicType) ?? throw new InvalidOperationException("Could not create an instance of the runtime type.");
            instance.DateValue = expected;

            Assert.AreEqual(expected, instance.DateValue);
        }

        [Test]
        public void WhenCreatingRuntimeTypeWithAttributeThenSetCustomAttribute()
        {
            ArgumentNullException.ThrowIfNull(_typeProvider);

            var properties = new[] { typeof(FakeItem).GetProperty("DateValue")! };

            var dynamicType = _typeProvider.Get(typeof(FakeItem), properties);

            var dataMemberAttribute = dynamicType
                .GetCustomAttributes(false);
            var data = dynamicType.GetCustomAttributesData();
            Assert.IsNotEmpty(dataMemberAttribute);
        }
    }
}