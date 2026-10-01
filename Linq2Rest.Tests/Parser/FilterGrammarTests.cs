using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// Filters split into conditions the same way whatever their shape: string literals in either quote style keep
    /// their parentheses and keywords to themselves, and a condition in parentheses reads like the bare condition.
    /// </summary>
    [TestFixture]
    public class FilterGrammarTests
    {
        private ODataExpressionConverter? _converter;
        private Record[]? _records;

        [SetUp]
        public void Setup()
        {
            _converter = new ODataExpressionConverter();
            _records = new[]
            {
                new Record { Id = 1, Status = "a (b", Priority = 1, IsActive = true },
                new Record { Id = 2, Status = ":)", Priority = 2, IsActive = false },
                new Record { Id = 3, Status = "x or y", Priority = 3, IsActive = true },
                new Record { Id = 4, Status = "it's (x)", Priority = 4, IsActive = false },
            };
        }

        [TestCase("Status eq 'a (b' or Priority eq 3 and IsActive", "1,3")]
        [TestCase("Status eq ':)' or Priority eq 1", "1,2")]
        [TestCase("not (Status eq ':)') and not IsActive", "4")]
        [TestCase("Status eq 'it''s (x)' or Priority eq 2", "2,4")]
        [TestCase("Status eq \"x or y\" or Priority eq 4 and not IsActive", "3,4")]
        [TestCase("Status eq \"a (b\" and IsActive", "1")]
        [TestCase("IsActive and Priority gt 1 and Status eq 'x or y'", "3")]
        [TestCase("startswith(Status, ':)') or endswith(Status, '(x)')", "2,4")]
        public void KeepsStringLiteralsWhole(string filter, string expectedIds)
        {
            AssertMatches(filter, expectedIds);
        }

        [TestCase("not (IsActive)", "2,4")]
        [TestCase("(IsActive) and Priority gt 1", "3")]
        [TestCase("Priority eq 1 or not (IsActive) and Priority eq 2", "1,2")]
        [TestCase("(Priority eq 1) or (Priority eq 2) and (IsActive)", "1")]
        [TestCase("((Priority eq 1))", "1")]
        [TestCase("((Priority eq 1) or (IsActive and Priority gt 2))", "1,3")]
        public void ReadsParenthesizedConditionsLikeBareOnes(string filter, string expectedIds)
        {
            AssertMatches(filter, expectedIds);
        }

        [TestCase("IsActive eq (true)", "1,3")]
        [TestCase("IsActive eq (1)", "1,3")]
        [TestCase("Priority eq (2)", "2")]
        [TestCase("((Priority add 1)) eq 3", "2")]
        [TestCase("(Priority) add 1 eq 3", "2")]
        [TestCase("Status in ('a (b', ':)')", "1,2")]
        public void ReadsParenthesizedValuesLikeBareOnes(string filter, string expectedIds)
        {
            AssertMatches(filter, expectedIds);
        }

        [TestCase("indexof(Status, ' (x)') add 1 eq 5", "4")]
        [TestCase("indexof(Status, 'a ( b )') add 1 eq 0", "1,2,3,4")]
        [TestCase("Priority add 1 eq 2 or indexof(Status, 'or y') add 1 eq 3", "1,3")]
        public void KeepsStringLiteralsWholeInArithmetic(string filter, string expectedIds)
        {
            AssertMatches(filter, expectedIds);
        }

        [TestCase("startswith(Status, 'x or')", "3")]
        [TestCase("startswith(Status, 'x or') or Priority eq 1", "1,3")]
        [TestCase("not startswith(Status, 'x or') and Priority lt 3", "1,2")]
        public void ReadsBooleanFunctionsWithKeywordsInLiterals(string filter, string expectedIds)
        {
            AssertMatches(filter, expectedIds);
        }

        [TestCase("Priority eq 1 or Priority eq 2 and IsActive or Priority eq 4 and not IsActive", "1,4")]
        [TestCase("Priority gt 1 and Priority lt 4 and IsActive or Priority eq 1 or not IsActive and Priority eq 2", "1,2,3")]
        [TestCase("IsActive and (Priority eq 1 or Priority eq 4) or Priority eq 2 and not IsActive", "1,2")]
        public void CombinesAndBeforeOrAcrossLongChains(string filter, string expectedIds)
        {
            AssertMatches(filter, expectedIds);
        }

        [TestCase("(Priority eq 1")]
        [TestCase("Priority eq 1)")]
        [TestCase("((Priority eq 1)")]
        [TestCase("Status eq 'a (b")]
        public void RejectsUnbalancedFilter(string filter)
        {
            ArgumentNullException.ThrowIfNull(_converter);

            Assert.Catch(() => _converter.Convert<Record>(filter));
        }

        private void AssertMatches(string filter, string expectedIds)
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

            public string Status { get; set; } = string.Empty;

            public int Priority { get; set; }

            public bool IsActive { get; set; }
        }
    }
}
