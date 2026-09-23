// --------------------------------------------------------------------------------------------------------------------
// <copyright file="JsonDataContractSerializerFactoryTests.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the JsonDataContractSerializerFactoryTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Tests.Implementations
{
    using LinqConvertTools.Implementations;
    using NUnit.Framework;
    using System;
    using System.Linq;

    [TestFixture]
    public class JsonDataContractSerializerFactoryTests
    {
        private JsonDataContractSerializerFactory? _factory;

        [SetUp]
        public void Setup()
        {
            _factory = new JsonDataContractSerializerFactory(Type.EmptyTypes);
        }

        [Test]
        public void CreatedSerializerCanDeserializeDataContractType()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            const string Json = "{\"Value\": 2, \"Text\":\"test\"}";

            var serializer = _factory.Create<SimpleContractItem>();

            var deserializedResult = serializer.Deserialize(Json.ToStream());

            Assert.AreEqual(2, deserializedResult.Value);
            Assert.AreEqual("test", deserializedResult.SomeString);
        }

        [Test]
        public void CreatedSerializerCanDeserializeListOfDataContractType()
        {
            ArgumentNullException.ThrowIfNull(_factory);

            const string Json = "[{\"Value\": 2, \"Text\":\"test\"}]";

            var serializer = _factory.Create<SimpleContractItem>();

            var deserializedResult = serializer.DeserializeList(Json.ToStream());

            Assert.AreEqual(1, deserializedResult.Count());
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
