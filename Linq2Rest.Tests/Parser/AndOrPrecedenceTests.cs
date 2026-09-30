using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// Filters follow OData operator precedence: <c>not</c> binds tightest, then <c>and</c>, then <c>or</c>, and
    /// parentheses override it. <c>A or B and C</c> means <c>A or (B and C)</c>.
    /// </summary>
    [TestFixture]
    public class AndOrPrecedenceTests
    {
        private ODataExpressionConverter? _converter;
        private Record[]? _records;

        [SetUp]
        public void Setup()
        {
            _converter = new ODataExpressionConverter();
            _records = new[]
            {
                new Record { Id = 1, Status = "Published", Priority = 1, IsActive = true },
                new Record { Id = 2, Status = "Draft", Priority = 2, IsActive = false },
                new Record { Id = 3, Status = "Archived", Priority = 3, IsActive = true },
                new Record { Id = 4, Status = "Published", Priority = 4, IsActive = false },
            };
        }

        [TestCase("Status eq 'Draft' or Priority eq 3 and IsActive", "2,3")]
        [TestCase("Status eq 'Draft' OR Priority eq 3 AND IsActive", "2,3")]
        [TestCase("IsActive and Priority eq 1 or Status eq 'Draft'", "1,2")]
        [TestCase("Priority lt 3 and IsActive or Priority gt 3 and not IsActive", "1,4")]
        [TestCase("Priority eq 1 or IsActive and Status eq 'Archived' or Priority eq 4", "1,3,4")]
        [TestCase("Status eq 'Draft' or Priority eq 1 or Priority eq 3 and IsActive and Priority gt 2", "1,2,3")]
        [TestCase("Priority gt 1 and Priority lt 4 and IsActive or Priority eq 1 and Status eq 'Published'", "1,3")]
        [TestCase("not IsActive or Priority eq 1 and not (Status eq 'Published')", "2,4")]
        [TestCase("not (Priority eq 2) and not IsActive or Status eq 'Archived'", "3,4")]
        [TestCase("startswith(Status, 'Dra') or Priority eq 3 and IsActive", "2,3")]
        [TestCase("Priority add 1 eq 3 or Priority mod 2 eq 1 and IsActive", "1,2,3")]
        [TestCase("3 eq Priority or 1 eq Priority and IsActive", "1,3")]
        [TestCase("(Status eq 'Draft' or Priority eq 3) and IsActive", "3")]
        [TestCase("Status eq 'Draft' or (Priority eq 3 and IsActive)", "2,3")]
        [TestCase("IsActive and (Priority eq 1 or Status eq 'Draft')", "1")]
        [TestCase("(IsActive and Priority eq 1) or Status eq 'Draft'", "1,2")]
        [TestCase("IsActive and (Priority lt 3 or Priority gt 3) or Status eq 'Draft'", "1,2")]
        [TestCase("(Priority eq 1 or Priority eq 2 and IsActive) and Status eq 'Published'", "1")]
        [TestCase("Priority eq 1 or Priority eq 2 or Priority eq 4", "1,2,4")]
        [TestCase("Priority gt 1 and Priority lt 4 and IsActive", "3")]
        public void AppliesAndBeforeOr(string filter, string expectedIds)
        {
            ArgumentNullException.ThrowIfNull(_converter);
            ArgumentNullException.ThrowIfNull(_records);

            var predicate = _converter.Convert<Record>(filter);

            var matches = _records.AsQueryable().Where(predicate).Select(r => r.Id).ToArray();

            CollectionAssert.AreEqual(expectedIds.Split(',').Select(int.Parse).ToArray(), matches, "Failed for " + predicate);
        }

        [TestCase("Priority eq 1 or Priority eq 2 and IsActive", "x => ((x.Priority == 1) OrElse ((x.Priority == 2) AndAlso x.IsActive))")]
        [TestCase("IsActive and Priority eq 1 or Priority eq 2", "x => ((x.IsActive AndAlso (x.Priority == 1)) OrElse (x.Priority == 2))")]
        [TestCase("Priority eq 1 and IsActive or Priority eq 2 and not IsActive",
            "x => (((x.Priority == 1) AndAlso x.IsActive) OrElse ((x.Priority == 2) AndAlso Not(x.IsActive)))")]
        [TestCase("(Priority eq 1 or Priority eq 2) and IsActive", "x => (((x.Priority == 1) OrElse (x.Priority == 2)) AndAlso x.IsActive)")]
        public void GroupsAndOperandsBeforeOr(string filter, string expected)
        {
            ArgumentNullException.ThrowIfNull(_converter);

            var predicate = _converter.Convert<Record>(filter);

            Assert.AreEqual(expected, predicate.ToString());
        }

        [TestCase("not (Status eq 'Draft' or 3 eq Priority)", "1,4")]
        [TestCase("IsActive and not (Status eq 'Draft' or 3 eq Priority)", "1")]
        [TestCase("not (Status eq 'Draft' and 3 eq Priority or IsActive)", "2,4")]
        [TestCase("Status eq 'Archived' or (Status eq 'Draft' and 2 eq Priority)", "2,3")]
        [TestCase("(Status eq 'Draft' and 2 eq Priority) or Status eq 'Archived' and IsActive", "2,3")]
        public void ReadsLiteralsInsideNegatedAndGroupedConditionsByTheirOwnComparison(string filter, string expectedIds)
        {
            ArgumentNullException.ThrowIfNull(_converter);
            ArgumentNullException.ThrowIfNull(_records);

            var predicate = _converter.Convert<Record>(filter);

            var matches = _records.AsQueryable().Where(predicate).Select(r => r.Id).ToArray();

            CollectionAssert.AreEqual(expectedIds.Split(',').Select(int.Parse).ToArray(), matches, "Failed for " + predicate);
        }

        [TestCase("Priority eq 1 or or IsActive and Priority eq 3")]
        [TestCase("Priority eq 1 and IsActive or")]
        [TestCase("or Priority eq 1 and IsActive")]
        [TestCase("Priority eq 1 and and IsActive or Priority eq 3")]
        [TestCase("Priority eq 1 and IsActive or and Priority eq 3")]
        public void RejectsMisplacedCombinerInMixedChain(string filter)
        {
            ArgumentNullException.ThrowIfNull(_converter);

            Assert.Throws<InvalidOperationException>(() => _converter.Convert<Record>(filter));
        }

        [Test]
        public void ParsesLongMixedChainWithinMaxDepth()
        {
            ArgumentNullException.ThrowIfNull(_converter);
            ArgumentNullException.ThrowIfNull(_records);

            string filter = string.Join(" or ", Enumerable.Range(0, 100).Select(i => "Priority eq " + (i + 10) + " and IsActive"))
                + " or Priority eq 3 and IsActive";

            var predicate = _converter.Convert<Record>(filter);

            CollectionAssert.AreEqual(new[] { 3 }, _records.AsQueryable().Where(predicate).Select(r => r.Id).ToArray());
        }

        public class Record
        {
            public int Id { get; set; }

            public string Status { get; set; } = string.Empty;

            public int Priority { get; set; }

            public bool IsActive { get; set; }
        }
    }
}
