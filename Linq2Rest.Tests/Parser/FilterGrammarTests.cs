using System.Linq.Expressions;
using LinqConvertTools.Parser.Readers;
using LinqConvertTools.Provider.Writers;
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
                new Record { Id = 1, Status = "a (b", Note = "n1", Priority = 1, IsActive = true, Price = 1.4, Kind = Kind.Alpha },
                new Record { Id = 2, Status = ":)", Priority = 2, IsActive = false, IsFlagged = true, Aliases = { ":)", "smile" }, Kind = Kind.Beta, Stage = Kind.Beta },
                new Record { Id = 3, Status = "x or y", Note = "it's", Priority = 3, IsActive = true, IsFlagged = false, Kind = Kind.Gamma },
                new Record { Id = 4, Status = "it's (x)", Priority = 4, IsActive = false, Kind = Kind.Alpha, Stage = Kind.Gamma },
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
        [TestCase("Priority in ((1), 2)", "1,2")]
        [TestCase("Status in (('a (b'), ':)')", "1,2")]
        [TestCase("IsFlagged eq (true)", "2")]
        [TestCase("Priority add (1) eq 2", "1")]
        [TestCase("Priority mul (1 add 1) eq 4", "2")]
        [TestCase("-(Priority) eq -1", "1")]
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

        [TestCase("substring(Status, Priority sub 1) eq 'or y'", "3")]
        [TestCase("round(Price mul 2) eq 3", "1")]
        public void ReadsArithmeticInsideFunctionArguments(string filter, string expectedIds)
        {
            AssertMatches(filter, expectedIds);
        }

        [TestCase("Status eq 'abc'def")]
        [TestCase("Status in ('a (b', ':)'")]
        [TestCase("Priority in (1, 2")]
        [TestCase("Status eq 'x or y' x")]
        public void RejectsTextAfterStringLiteral(string filter)
        {
            ArgumentNullException.ThrowIfNull(_converter);

            Assert.Throws<FormatException>(() => _converter.Convert<Record>(filter));
        }

        [TestCase("IsActive eq 12")]
        [TestCase("IsActive eq untrue")]
        [TestCase("IsFlagged eq untrue")]
        public void RejectsValueThatIsNotBoolean(string filter)
        {
            ArgumentNullException.ThrowIfNull(_converter);

            var exception = Assert.Throws<InvalidOperationException>(() => _converter.Convert<Record>(filter));
            StringAssert.Contains("as boolean", exception!.Message);
        }

        [TestCase("Note eq 'it's'", "3")]
        [TestCase("(Note eq 'it's')", "3")]
        [TestCase("(Priority eq 1) or (Note eq 'it's')", "1,3")]
        [TestCase("Note eq 'it's' or Priority eq 2", "2,3")]
        [TestCase("Note in ('it's', 'n1')", "1,3")]
        [TestCase("Status eq 'it's (x)'", "4")]
        public void ReadsUnescapedApostrophesInLiterals(string filter, string expectedIds)
        {
            AssertMatches(filter, expectedIds);
        }

        [Test]
        public void ReadsLiteralContainingArithmeticWord()
        {
            ArgumentNullException.ThrowIfNull(_converter);
            var rows = new[]
            {
                new Record { Id = 1, Note = "Don't add sugar" },
                new Record { Id = 2, Note = "sugar" },
            };

            var predicate = _converter.Convert<Record>("Note eq 'Don't add sugar'");

            CollectionAssert.AreEqual(new[] { 1 }, rows.AsQueryable().Where(predicate).Select(r => r.Id).ToArray());
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

        [TestCase("()")]
        [TestCase("( )")]
        [TestCase("(())")]
        [TestCase("Priority eq ()")]
        [TestCase("not ()")]
        [TestCase("() and IsActive")]
        public void RejectsEmptyParentheses(string filter)
        {
            ArgumentNullException.ThrowIfNull(_converter);

            Assert.Throws<InvalidOperationException>(() => _converter.Convert<Record>(filter));
        }

        [TestCase("Status in ()", "")]
        [TestCase("Status in ('a,b', ':)')", "2")]
        [TestCase("Status in (\"x or y\", 'it''s (x)')", "3,4")]
        [TestCase("Status in ':)'", "2")]
        [TestCase("Note in ('n1', null)", "1,2,4")]
        [TestCase("Status in (x or y, ':)')", "2,3")]
        [TestCase("Status in (Status)", "")]
        [TestCase("Note in (Note)", "")]
        [TestCase("Status in Aliases", "2")]
        [TestCase("startswith(Status, \"a,b\") or Priority eq 4", "4")]
        [TestCase("startswith(Status, \"x or\")", "3")]
        public void ReadsInListsAndFunctionArgumentsWithCommasAndQuotes(string filter, string expectedIds)
        {
            AssertMatches(filter, expectedIds);
        }

        [Test]
        public void ComparesInListsIgnoringCase()
        {
            ArgumentNullException.ThrowIfNull(_converter);
            ArgumentNullException.ThrowIfNull(_records);

            var predicate = _converter.Convert<Record>("Status in ('A (B', 'X OR Y')", true);

            CollectionAssert.AreEqual(new[] { 1, 3 }, _records.AsQueryable().Where(predicate).Select(r => r.Id).ToArray());
        }

        [TestCase("Priority in (1), (3)")]
        [TestCase("Status in (a (b, ':)')")]
        public void RejectsMalformedInList(string filter)
        {
            ArgumentNullException.ThrowIfNull(_converter);

            Assert.Throws<FormatException>(() => _converter.Convert<Record>(filter));
        }

        [Test]
        public void MatchesNullInListsIgnoringCase()
        {
            ArgumentNullException.ThrowIfNull(_converter);
            ArgumentNullException.ThrowIfNull(_records);

            var predicate = _converter.Convert<Record>("Note in ('N1', null)", true);

            CollectionAssert.AreEqual(new[] { 1, 2, 4 }, _records.AsQueryable().Where(predicate).Select(r => r.Id).ToArray());
        }

        [TestCase("Note eq 'N1'", "1")]
        [TestCase("Note ne 'N1'", "2,3,4")]
        [TestCase("Note eq null", "2,4")]
        [TestCase("Note ne null", "1,3")]
        public void ComparesNullableMemberIgnoringCase(string filter, string expectedIds)
        {
            ArgumentNullException.ThrowIfNull(_converter);
            ArgumentNullException.ThrowIfNull(_records);

            var predicate = _converter.Convert<Record>(filter, true);

            var matches = _records.AsQueryable().Where(predicate).Select(r => r.Id).ToArray();
            var expected = expectedIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
            CollectionAssert.AreEqual(expected, matches, "Failed for " + predicate);
        }

        [Test]
        public void ComparesInCollectionMemberIgnoringCase()
        {
            ArgumentNullException.ThrowIfNull(_converter);
            var records = new[]
            {
                new Record { Id = 1, Status = "EMAIL", Aliases = { "email", "finance" } },
                new Record { Id = 2, Status = "legal", Aliases = { "LEGAL" } },
                new Record { Id = 3, Status = "memo", Aliases = { "draft", null! } },
                new Record { Id = 4, Status = "none", Aliases = { } },
            };

            var predicate = _converter.Convert<Record>("Status in Aliases", true);

            CollectionAssert.AreEqual(new[] { 1, 2 }, records.AsQueryable().Where(predicate).Select(r => r.Id).ToArray());
        }

        [TestCase("Kind eq 'Alpha'", "1,4")]
        [TestCase("Kind eq 'alpha'", "1,4")]
        [TestCase("Kind eq \"Beta\"", "2")]
        [TestCase("Kind eq Gamma", "3")]
        [TestCase("Kind ne 'Alpha'", "2,3")]
        [TestCase("Kind in ('Beta','Gamma')", "2,3")]
        public void ReadsFriendlyEnumLiterals(string filter, string expectedIds)
        {
            AssertMatches(filter, expectedIds);
        }

        [TestCase("Stage eq 'Gamma'", "4")]
        [TestCase("Stage eq null", "1,3")]
        [TestCase("Stage ne null", "2,4")]
        public void ReadsFriendlyNullableEnumLiterals(string filter, string expectedIds)
        {
            AssertMatches(filter, expectedIds);
        }

        [TestCase("Note eq 'it''s' and Kind eq 'Gamma'", "3")]
        [TestCase("Status eq \"x or y\" and Kind eq 'Gamma'", "3")]
        [TestCase("not (Kind eq 'Alpha') and Priority gt 1", "2,3")]
        public void ReadsEnumClauseAlongsideStringAndPrecedence(string filter, string expectedIds)
        {
            AssertMatches(filter, expectedIds);
        }

        [Test]
        public void RejectsNumericAndUndefinedEnumWhenNamesOnly()
        {
            ArgumentNullException.ThrowIfNull(_records);
            var converter = new ODataExpressionConverter(Array.Empty<IValueWriter>(), Array.Empty<IValueExpressionFactory>(), null, StringCaseFolding.CurrentCulture, enumNamesOnly: true);

            Assert.Throws<FormatException>(() => converter.Convert<Record>("Kind eq '1'"));
            Assert.Throws<FormatException>(() => converter.Convert<Record>("Kind eq 'Nope'"));
            Assert.DoesNotThrow(() => converter.Convert<Record>("Kind eq 'Alpha'"));
        }

        [Test]
        public void ReadsNumericEnumWhenNamesOnlyIsOff()
        {
            AssertMatches("Kind eq '1'", "1,4");
        }

        [TestCase(200, "1")]
        [TestCase(300, null)]
        [TestCase(40000, null)]
        public void CountsEnclosingParenthesesTowardMaxDepth(int levels, string? expectedIds)
        {
            ArgumentNullException.ThrowIfNull(_converter);
            var filter = new string('(', levels) + "Priority eq 1" + new string(')', levels);

            if (expectedIds is null)
            {
                Assert.Throws<InvalidOperationException>(() => _converter.Convert<Record>(filter));
            }
            else
            {
                AssertMatches(filter, expectedIds);
            }
        }

        [Test, Timeout(10000)]
        public void RejectsDeeplyNestedArithmeticWithoutRescanningEachLevel()
        {
            ArgumentNullException.ThrowIfNull(_converter);
            var filter = string.Concat(Enumerable.Repeat("1 add ", 4000)) + "1 eq 5";

            Assert.Throws<InvalidOperationException>(() => _converter.Convert<Record>(filter));
        }

        [TestCase("IsActive eq yes", new[] { 1, 3 })]
        [TestCase("IsFlagged eq yes", new[] { 2 })]
        public void ReadsBooleanWithCustomFactory(string filter, int[] expectedIds)
        {
            ArgumentNullException.ThrowIfNull(_records);
            var converter = new ODataExpressionConverter(Array.Empty<IValueWriter>(), new[] { new YesNoFactory() });

            var predicate = converter.Convert<Record>(filter);

            CollectionAssert.AreEqual(expectedIds, _records.AsQueryable().Where(predicate).Select(r => r.Id).ToArray());
        }

        [Test]
        public void ReadsNullableBooleanWithCustomFactory()
        {
            ArgumentNullException.ThrowIfNull(_records);
            var converter = new ODataExpressionConverter(Array.Empty<IValueWriter>(), new[] { new UnknownAsNullFactory() });

            var predicate = converter.Convert<Record>("IsFlagged eq unknown");

            CollectionAssert.AreEqual(new[] { 1, 4 }, _records.AsQueryable().Where(predicate).Select(r => r.Id).ToArray());
        }

        private void AssertMatches(string filter, string expectedIds)
        {
            ArgumentNullException.ThrowIfNull(_converter);
            ArgumentNullException.ThrowIfNull(_records);

            var predicate = _converter.Convert<Record>(filter);

            var matches = _records.AsQueryable().Where(predicate).Select(r => r.Id).ToArray();

            var expected = expectedIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
            CollectionAssert.AreEqual(expected, matches, "Failed for " + predicate);
        }

        public class Record
        {
            public int Id { get; set; }

            public string Status { get; set; } = string.Empty;

            public string? Note { get; set; }

            public int Priority { get; set; }

            public bool IsActive { get; set; }

            public bool? IsFlagged { get; set; }

            public double Price { get; set; }

            public List<string> Aliases { get; set; } = new();

            public Kind Kind { get; set; }

            public Kind? Stage { get; set; }
        }

        public enum Kind
        {
            Unknown,
            Alpha,
            Beta,
            Gamma,
        }

        private sealed class YesNoFactory : IValueExpressionFactory
        {
            public bool Handles(Type type)
            {
                return type == typeof(bool);
            }

            public ConstantExpression Convert(string token)
            {
                return Expression.Constant(string.Equals(token, "yes", StringComparison.OrdinalIgnoreCase));
            }
        }

        private sealed class UnknownAsNullFactory : IValueExpressionFactory
        {
            public bool Handles(Type type)
            {
                return type == typeof(bool?);
            }

            public ConstantExpression Convert(string token)
            {
                return string.Equals(token, "unknown", StringComparison.OrdinalIgnoreCase)
                    ? Expression.Constant(null, typeof(bool?))
                    : Expression.Constant(bool.Parse(token), typeof(bool?));
            }
        }
    }
}
