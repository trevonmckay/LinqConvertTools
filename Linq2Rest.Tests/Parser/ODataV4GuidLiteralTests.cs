using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// OData v4 writes GUIDs as bare literals — <c>Id eq 0f000000-0000-7000-8000-000000000001</c> — rather than the v3
    /// typed form <c>guid'0f000000-0000-7000-8000-000000000001'</c>. Both must convert, for nullable and non-nullable members.
    /// </summary>
    [TestFixture]
    public class ODataV4GuidLiteralTests
    {
        private static readonly Guid DigitLed = Guid.Parse("0f000000-0000-7000-8000-000000000001");
        private static readonly Guid LetterLed = Guid.Parse("deadbeef-0000-7000-8000-000000000002");

        private ODataExpressionConverter _converter = null!;
        private Record[] _records = null!;

        [SetUp]
        public void Setup()
        {
            _converter = new ODataExpressionConverter();
            _records = new[]
            {
                new Record { Id = DigitLed, ParentId = LetterLed },
                new Record { Id = LetterLed, ParentId = null },
            };
        }

        [TestCase("Id eq 0f000000-0000-7000-8000-000000000001", "0f000000-0000-7000-8000-000000000001")]
        [TestCase("Id eq guid'0f000000-0000-7000-8000-000000000001'", "0f000000-0000-7000-8000-000000000001")]
        [TestCase("Id eq deadbeef-0000-7000-8000-000000000002", "deadbeef-0000-7000-8000-000000000002")]
        [TestCase("Id eq DEADBEEF-0000-7000-8000-000000000002", "deadbeef-0000-7000-8000-000000000002")]
        [TestCase("Id ne 0f000000-0000-7000-8000-000000000001", "deadbeef-0000-7000-8000-000000000002")]
        [TestCase("ParentId eq deadbeef-0000-7000-8000-000000000002", "0f000000-0000-7000-8000-000000000001")]
        [TestCase("ParentId eq guid'deadbeef-0000-7000-8000-000000000002'", "0f000000-0000-7000-8000-000000000001")]
        public void FiltersByGuidLiteral(string filter, string expectedId)
        {
            var predicate = _converter.Convert<Record>(filter);

            var matches = _records.AsQueryable().Where(predicate).Select(r => r.Id).ToArray();

            CollectionAssert.AreEqual(new[] { Guid.Parse(expectedId) }, matches, "Failed for " + predicate);
        }

        [TestCase("Id eq 0f000000-0000-7000-8000-00000000001")]
        [TestCase("Id eq 0f000000000070008000000000000001")]
        [TestCase("Id eq {0f000000-0000-7000-8000-000000000001}")]
        public void RejectsMalformedBareGuid(string filter)
        {
            Assert.Catch(() => _converter.Convert<Record>(filter));
        }

        public class Record
        {
            public Guid Id { get; set; }

            public Guid? ParentId { get; set; }
        }
    }
}
