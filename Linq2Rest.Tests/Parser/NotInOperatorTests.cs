namespace LinqConvertTools.Tests.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;

    /// <summary>
    /// Behavioural coverage for <c>member not in (list)</c>, accepted as shorthand for the standard OData
    /// <c>not (member in (list))</c>, at the top level and inside <c>any</c>/<c>all</c> lambdas.
    /// </summary>
    [TestFixture]
    public class NotInOperatorTests
    {
        private sealed class ChildRow
        {
            public string? Code { get; set; }
        }

        private sealed class Row
        {
            public string Id { get; set; } = string.Empty;

            public string? CountryCode { get; set; }

            public List<string> Tags { get; set; } = new List<string>();

            public List<string> Codes { get; set; } = new List<string>();

            public ChildRow? Child { get; set; }
        }

        private static readonly Row[] Rows =
        {
            new Row { Id = "SA", CountryCode = "SA", Tags = new List<string> { "a", "b" }, Codes = new List<string> { "SA" }, Child = new ChildRow { Code = "GB" } },
            new Row { Id = "AE", CountryCode = "AE", Tags = new List<string> { "c" }, Codes = new List<string> { "GB" }, Child = new ChildRow { Code = "AE" } },
            new Row { Id = "GB", CountryCode = "GB", Tags = new List<string> { "a" }, Codes = new List<string> { "GB" }, Child = null },
            new Row { Id = "none", CountryCode = null, Tags = new List<string>(), Codes = new List<string>(), Child = new ChildRow { Code = null } },
        };

        private static string Match(string filter, bool ignoreCase = false)
        {
            var predicate = new ODataExpressionConverter().Convert<Row>(filter, ignoreCase).Compile();
            return string.Join(",", Rows.Where(predicate).Select(r => r.Id).OrderBy(id => id, StringComparer.Ordinal));
        }

        [Test]
        [TestCase("countryCode not in ('GB')", "AE,SA,none")]
        [TestCase("countryCode not in ('GB', 'SA')", "AE,none")]
        [TestCase("countryCode not in ('GB') and countryCode ne null", "AE,SA")]
        [TestCase("countryCode eq 'GB' or countryCode not in ('GB', 'SA', 'AE')", "GB,none")]
        [TestCase("tags/any(t: t not in ('a'))", "AE,SA")]
        [TestCase("tags/all(t: t not in ('a'))", "AE,none")]
        public void NotIn_MatchesRowsOutsideTheList(string filter, string expected)
        {
            Assert.AreEqual(expected, Match(filter));
        }

        [Test]
        public void NotIn_HonoursIgnoreCase()
        {
            Assert.AreEqual("AE,SA,none", Match("countryCode not in ('gb')", ignoreCase: true));
        }

        [Test]
        [TestCase("countryCode not in ('GB', 'SA')", "not (countryCode in ('GB', 'SA'))")]
        [TestCase("countryCode NOT IN ('GB')", "not (countryCode in ('GB'))")]
        [TestCase("countryCode NoT In ('GB')", "not (countryCode in ('GB'))")]
        [TestCase("countryCode not in codes", "not (countryCode in codes)")]
        [TestCase("child/code not in ('GB')", "not (child/code in ('GB'))")]
        [TestCase("tolower(countryCode) not in ('gb')", "not (tolower(countryCode) in ('gb'))")]
        [TestCase("not countryCode not in ('GB')", "not (not (countryCode in ('GB')))")]
        [TestCase("tags/any(t: t not in ('a'))", "tags/any(t: not (t in ('a')))")]
        public void NotIn_MatchesTheStandardNegatedForm(string shorthand, string standard)
        {
            Assert.AreEqual(Match(standard), Match(shorthand));
        }

        [Test]
        public void NotIn_OnANestedMember_IncludesRowsWhoseParentIsNull()
        {
            // As with the standard not (child/code in (...)), a null parent leaves the member outside the list, so the
            // row matches. A comparison such as child/code ne 'GB' instead guards the parent and excludes the row.
            Assert.AreEqual("AE,GB,none", Match("child/code not in ('GB')"));
            Assert.AreEqual("AE,none", Match("child/code ne 'GB'"));
        }

        [Test]
        [TestCase("countryCode not in")]
        [TestCase("countryCode not eq 'GB'")]
        public void NotWithoutAFollowingInList_IsRejected(string filter)
        {
            Assert.Throws<InvalidOperationException>(() => new ODataExpressionConverter().Convert<Row>(filter, false));
        }
    }
}
