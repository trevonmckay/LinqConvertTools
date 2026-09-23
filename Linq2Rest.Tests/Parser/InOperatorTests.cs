using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// The <c>in</c> operator reads each list item as a literal of the member's type, so it filters members of any
    /// literal type, not only strings.
    /// </summary>
    [TestFixture]
    public class InOperatorTests
    {
        private static readonly Guid First = Guid.Parse("0f000000-0000-7000-8000-000000000001");
        private static readonly Guid Second = Guid.Parse("deadbeef-0000-7000-8000-000000000002");
        private static readonly Guid Third = Guid.Parse("0f000000-0000-7000-8000-000000000003");

        private ODataExpressionConverter? _converter;
        private Record[]? _records;

        [SetUp]
        public void Setup()
        {
            _converter = new ODataExpressionConverter();
            _records = new[]
            {
                new Record { Id = First, ParentId = Second, Count = 1 },
                new Record { Id = Second, ParentId = null, Count = 2 },
                new Record { Id = Third, ParentId = First, Count = 3 },
            };
        }

        [TestCase("Id in (0f000000-0000-7000-8000-000000000001, deadbeef-0000-7000-8000-000000000002)", "0f000000-0000-7000-8000-000000000001,deadbeef-0000-7000-8000-000000000002")]
        [TestCase("Id in (guid'0f000000-0000-7000-8000-000000000003')", "0f000000-0000-7000-8000-000000000003")]
        [TestCase("Id in (DEADBEEF-0000-7000-8000-000000000002,guid'0f000000-0000-7000-8000-000000000003')", "deadbeef-0000-7000-8000-000000000002,0f000000-0000-7000-8000-000000000003")]
        [TestCase("not (Id in (0f000000-0000-7000-8000-000000000001))", "deadbeef-0000-7000-8000-000000000002,0f000000-0000-7000-8000-000000000003")]
        [TestCase("ParentId in (deadbeef-0000-7000-8000-000000000002, 0f000000-0000-7000-8000-000000000001)", "0f000000-0000-7000-8000-000000000001,0f000000-0000-7000-8000-000000000003")]
        [TestCase("ParentId in (null)", "deadbeef-0000-7000-8000-000000000002")]
        [TestCase("Count in (1, 3)", "0f000000-0000-7000-8000-000000000001,0f000000-0000-7000-8000-000000000003")]
        [TestCase("Count in (1, 2) and Id in (deadbeef-0000-7000-8000-000000000002)", "deadbeef-0000-7000-8000-000000000002")]
        public void FiltersByListOfLiterals(string filter, string expectedIds)
        {
            ArgumentNullException.ThrowIfNull(_converter);
            ArgumentNullException.ThrowIfNull(_records);

            var predicate = _converter.Convert<Record>(filter);

            var matches = _records.AsQueryable().Where(predicate).Select(r => r.Id).ToArray();

            CollectionAssert.AreEqual(expectedIds.Split(',').Select(Guid.Parse).ToArray(), matches, "Failed for " + predicate);
        }

        [TestCase("Id in (0f000000-0000-7000-8000-000000000001, 'sales')")]
        [TestCase("Id in (null)")]
        [TestCase("Count in (1, 'many')")]
        public void RejectsItemThatIsNotALiteralOfTheMemberType(string filter)
        {
            ArgumentNullException.ThrowIfNull(_converter);

            Assert.Catch(() => _converter.Convert<Record>(filter));
        }

        public class Record
        {
            public Guid Id { get; set; }

            public Guid? ParentId { get; set; }

            public int Count { get; set; }
        }
    }
}
