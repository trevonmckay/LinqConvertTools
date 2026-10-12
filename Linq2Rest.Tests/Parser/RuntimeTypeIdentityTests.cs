using System.Runtime.Serialization;
using LinqConvertTools.Parser;
using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// A $select over types that share a short name gets a runtime type of its own, with its own member types and
    /// attributes.
    /// </summary>
    [TestFixture]
    public class RuntimeTypeIdentityTests
    {
        [Test]
        public void ClosedGenericTypesGetTheirOwnRuntimeType()
        {
            var ints = Select(new Wrapper<int> { Value = 42 }, "Value");
            var strings = Select(new Wrapper<string> { Value = "x" }, "Value");

            Assert.AreEqual(42, ints.GetType().GetProperty("Value")!.GetValue(ints));
            Assert.AreEqual("x", strings.GetType().GetProperty("Value")!.GetValue(strings));
            Assert.AreNotSame(ints.GetType(), strings.GetType());
        }

        [Test]
        public void TypesWithTheSameNameInDifferentScopesKeepTheirOwnAttributes()
        {
            var first = Select(new FirstScope.Customer { Id = 1 }, "Id");
            var second = Select(new SecondScope.Customer { Id = 2 }, "Id");

            Assert.AreNotSame(first.GetType(), second.GetType());
            Assert.AreEqual("FirstCustomer", ContractName(first.GetType()));
            Assert.AreEqual("SecondCustomer", ContractName(second.GetType()));
        }

        [Test]
        public void SameShapeIsBuiltOnce()
        {
            var a = Select(new Wrapper<long> { Value = 1 }, "Value");
            var b = Select(new Wrapper<long> { Value = 2 }, "Value");

            Assert.AreSame(a.GetType(), b.GetType());
        }

        private static object Select<T>(T item, string selection)
        {
            var resolver = new MemberNameResolver();
            var factory = new SelectExpressionFactory<T>(resolver, new RuntimeTypeProvider(resolver));
            return factory.Create(selection)!.Compile()(item);
        }

        private static string? ContractName(Type type)
        {
            return type.GetCustomAttributes(typeof(DataContractAttribute), false).OfType<DataContractAttribute>().Single().Name;
        }

        public class Wrapper<TValue>
        {
            public TValue? Value { get; set; }
        }

        public static class FirstScope
        {
            [DataContract(Name = "FirstCustomer")]
            public class Customer
            {
                [DataMember]
                public int Id { get; set; }
            }
        }

        public static class SecondScope
        {
            [DataContract(Name = "SecondCustomer")]
            public class Customer
            {
                [DataMember]
                public int Id { get; set; }
            }
        }
    }
}
