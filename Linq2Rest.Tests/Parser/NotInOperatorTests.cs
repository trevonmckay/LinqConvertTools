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
        private sealed class Row
        {
            public string Id { get; set; } = string.Empty;

            public string? CountryCode { get; set; }

            public List<string> Tags { get; set; } = new List<string>();
        }

        private static readonly Row[] Rows =
        {
            new Row { Id = "SA", CountryCode = "SA", Tags = new List<string> { "a", "b" } },
            new Row { Id = "AE", CountryCode = "AE", Tags = new List<string> { "c" } },
            new Row { Id = "GB", CountryCode = "GB", Tags = new List<string> { "a" } },
            new Row { Id = "none", CountryCode = null, Tags = new List<string>() },
        };

        private static string Match(string filter)
        {
            var predicate = new ODataExpressionConverter().Convert<Row>(filter, false).Compile();
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
        [TestCase("countryCode not in ('GB', 'SA')", "not (countryCode in ('GB', 'SA'))")]
        [TestCase("tags/any(t: t not in ('a'))", "tags/any(t: not (t in ('a')))")]
        public void NotIn_MatchesTheStandardNegatedForm(string shorthand, string standard)
        {
            Assert.AreEqual(Match(standard), Match(shorthand));
        }
    }
}
