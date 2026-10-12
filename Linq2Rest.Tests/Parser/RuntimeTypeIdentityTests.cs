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

        [Test]
        public void AttributesWithArrayArgumentsAndNamedFieldsAreCopied()
        {
            var selected = Select(new Tagged { Id = 7 }, "Id");

            var tags = (TagsAttribute)selected.GetType().GetProperty("Id")!.GetCustomAttributes(typeof(TagsAttribute), false).Single();
            CollectionAssert.AreEqual(new[] { "a", "b" }, tags.Values);
            CollectionAssert.AreEqual(new[] { 1, 2 }, tags.Numbers);
            Assert.AreEqual("note", tags.Note);
            Assert.AreEqual(7, selected.GetType().GetProperty("Id")!.GetValue(selected));
        }

        [Test]
        public void SelectedPropertiesKeepTheirCultureOrder()
        {
            var selected = Select(new Cased(), "Beta,alpha");

            CollectionAssert.AreEqual(new[] { "alpha", "Beta" }, selected.GetType().GetProperties().Select(p => p.Name).ToArray());
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

        [AttributeUsage(AttributeTargets.Property)]
        public sealed class TagsAttribute : Attribute
        {
            public TagsAttribute(params string[] values)
            {
                Values = values;
            }

            public string[] Values { get; }

            public int[]? Numbers { get; set; }

#pragma warning disable CA1051
            public string? Note;
#pragma warning restore CA1051
        }

        public class Tagged
        {
            [Tags("a", "b", Numbers = new[] { 1, 2 }, Note = "note")]
            public int Id { get; set; }
        }

        public class Cased
        {
            [DataMember(Name = "alpha")]
            public int First { get; set; }

            [DataMember(Name = "Beta")]
            public int Second { get; set; }
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
