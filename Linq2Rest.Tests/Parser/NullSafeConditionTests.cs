using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// A condition that reads a member of a nested object is false when that object is null. Each such condition
    /// is guarded on its own, so it neither throws nor decides the conditions combined with it.
    /// </summary>
    [TestFixture]
    public class NullSafeConditionTests
    {
        private ODataExpressionConverter? _converter;
        private Record[]? _records;

        [SetUp]
        public void Setup()
        {
            _converter = new ODataExpressionConverter();
            _records = new[]
            {
                new Record { Id = 1, Child = null, IsActive = true, Priority = 1 },
                new Record { Id = 2, Child = new Child { Active = true }, IsActive = true, Priority = 2 },
                new Record { Id = 3, Child = new Child { Active = false }, IsActive = false, Priority = 3 },
            };
        }

        [TestCase("IsActive and Child/Active or Priority eq 9", "2")]
        [TestCase("Priority gt 1 and Child/Active", "2")]
        [TestCase("Child/Active and IsActive", "2")]
        [TestCase("Child/Active or Priority eq 1", "1,2")]
        [TestCase("Child/Active or Priority eq 1 and IsActive", "1,2")]
        [TestCase("Priority eq 3 or Child/Active", "2,3")]
        [TestCase("not Child/Active", "1,3")]
        [TestCase("not Child/Active or Priority eq 9", "1,3")]
        public void TreatsMemberOfNullObjectAsFalse(string filter, string expectedIds)
        {
            ArgumentNullException.ThrowIfNull(_converter);
            ArgumentNullException.ThrowIfNull(_records);

            var predicate = _converter.Convert<Record>(filter);

            var matches = _records.AsQueryable().Where(predicate).Select(r => r.Id).ToArray();

            CollectionAssert.AreEqual(expectedIds.Split(',').Select(int.Parse).ToArray(), matches, "Failed for " + predicate);
        }

        public class Record
        {
            public int Id { get; set; }

            public Child? Child { get; set; }

            public bool IsActive { get; set; }

            public int Priority { get; set; }
        }

        public class Child
        {
            public bool Active { get; set; }
        }
    }
}
