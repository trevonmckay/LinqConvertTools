using LinqConvertTools.Parser;
using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// The query options read only members that hold a readable value.
    /// </summary>
    [TestFixture]
    public class QueryableMemberTests
    {
        [TestCase("Item")]
        [TestCase("WriteOnly")]
        public void SelectOfAMemberWithoutAReadableValueIsNotRecognized(string selection)
        {
            var exception = ParseAssert.Throws<FormatException>(() => CreateSelectFactory<Row>().Create(selection));

            StringAssert.Contains(selection + " is not recognized as a valid property", exception.Message);
        }

        [TestCase("Item eq 1")]
        [TestCase("WriteOnly eq 1")]
        public void FilterOnAMemberWithoutAReadableValueIsInvalid(string filter)
        {
            var exception = Assert.Throws<ODataParseException>(() => new ODataExpressionConverter().Convert<Row>(filter))!;

            Assert.AreEqual(ODataParseErrorKind.Invalid, exception.Kind);
        }

        [Test]
        public void SelectOfReadableMembersStillWorks()
        {
            var selector = CreateSelectFactory<Row>().Create("Id,Name");

            Assert.IsNotNull(selector);
        }

        private static SelectExpressionFactory<T> CreateSelectFactory<T>()
        {
            var resolver = new MemberNameResolver();
            return new SelectExpressionFactory<T>(resolver, new RuntimeTypeProvider(resolver));
        }

        public class Row
        {
            public int Id { get; set; }

            public string? Name { get; set; }

            public int WriteOnly
            {
                set => Id = value;
            }

            public int this[int index] => index;
        }
    }
}
