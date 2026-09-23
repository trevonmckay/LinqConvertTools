// --------------------------------------------------------------------------------------------------------------------
// <copyright file="XmlSerializerFactoryTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the XmlSerializerFactoryTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests.Implementations
{
    using LinqConvertTools.Implementations;
    using NUnit.Framework;
    using System;
    using System.Linq;

    [TestFixture]
    public class XmlSerializerFactoryTests
    {
        private XmlSerializerFactory? _factory;

        [SetUp]
        public void Setup()
        {
            _factory = new XmlSerializerFactory(Type.EmptyTypes);
        }

        [Test]
        public void CreatedSerializerCanDeserializeListOfType()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            const string Xml = "<ArrayOfSimpleContractItem><SimpleContractItem><Text>test</Text><Value>2</Value></SimpleContractItem></ArrayOfSimpleContractItem>";

            var serializer = _factory.Create<SimpleContractItem>();

            var deserializedResult = serializer.DeserializeList(Xml.ToStream());

            Assert.AreEqual(1, deserializedResult.Count());
        }

        [Test]
        public void CreatedSerializerCanDeserializeType()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            const string Xml = "<SimpleContractItem><Text>test</Text><Value>2</Value></SimpleContractItem>";

            var serializer = _factory.Create<SimpleContractItem>();

            var deserializedResult = serializer.Deserialize(Xml.ToStream());

            Assert.AreEqual(2, deserializedResult.Value);
            Assert.AreEqual("test", deserializedResult.SomeString);
        }

        [Test]
        public void CreatedSerializerCanSerializeDataContractType()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            var serializer = _factory.Create<SimpleContractItem>();

            var deserializedResult = serializer.Serialize(new SimpleContractItem());

            Assert.NotNull(deserializedResult);
        }

        [Test]
        public void WhenCreatingSerializerThenDoesNotReturnNull()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            Assert.NotNull(_factory.Create<SimpleContractItem>());
        }
    }
}