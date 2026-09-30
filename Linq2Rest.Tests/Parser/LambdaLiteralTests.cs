using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// Literals in an any/all lambda body are read by the comparison they appear in, not by the type of the
    /// collection the lambda runs over.
    /// </summary>
    [TestFixture]
    public class LambdaLiteralTests
    {
        private ODataExpressionConverter? _converter;
        private Record[]? _records;

        [SetUp]
        public void Setup()
        {
            _converter = new ODataExpressionConverter();
            _records = new[]
            {
                new Record { Id = 1, Children = { new Child { Id = 1, Active = true } }, Numbers = { 1, 5 } },
                new Record { Id = 2, Children = { new Child { Id = 1, Active = false }, new Child { Id = 2, Active = true } }, Numbers = { 2 } },
                new Record { Id = 3, Numbers = { -1 } },
            };
        }

        [TestCase("Children/any(c: 1 eq c/Id)", "1,2")]
        [TestCase("Children/any(c: 1 eq c/Id and c/Active)", "1")]
        [TestCase("Children/any(c: c/Active and 2 eq c/Id)", "2")]
        [TestCase("Children/any(c: 1 eq c/Id or c/Id eq 2 and c/Active)", "1,2")]
        [TestCase("Children/all(c: 1 le c/Id and c/Active)", "1,3")]
        [TestCase("Numbers/any(n: 1 eq n and n gt 0)", "1")]
        [TestCase("Numbers/any(n: n gt 0 and 5 eq n)", "1")]
        [TestCase("Numbers/any(n: not (0 lt n))", "3")]
        public void ReadsLiteralsByTheirComparison(string filter, string expectedIds)
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

            public List<Child> Children { get; set; } = new();

            public List<int> Numbers { get; set; } = new();
        }

        public class Child
        {
            public int Id { get; set; }

            public bool Active { get; set; }
        }
    }
}
