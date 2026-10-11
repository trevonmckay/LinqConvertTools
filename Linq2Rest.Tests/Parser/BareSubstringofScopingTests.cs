namespace LinqConvertTools.Tests.Parser
{
    using System.Linq;
    using NUnit.Framework;

    /// <summary>
    /// Behavioural coverage for a bare, boolean <c>substringof()</c> combined with other conditions. The null guard
    /// that protects the call from a null member must scope to the call alone, so a row whose member is null is
    /// decided by the other branches of the combination and never throws.
    /// </summary>
    [TestFixture]
    public class BareSubstringofScopingTests
    {
        private sealed class Row
        {
            public string Id { get; set; } = string.Empty;

            public string? CountryCode { get; set; }
        }

        private static readonly Row[] Rows =
        {
            new Row { Id = "SA", CountryCode = "SA" },
            new Row { Id = "AE", CountryCode = "AE" },
            new Row { Id = "GB", CountryCode = "GB" },
            new Row { Id = "none", CountryCode = null },
        };

        private static string Match(string filter)
        {
            var predicate = new ODataExpressionConverter().Convert<Row>(filter, false).Compile();
            return string.Join(",", Rows.Where(predicate).Select(r => r.Id).OrderBy(id => id, System.StringComparer.Ordinal));
        }

        [Test]
        public void BareSubstringofOrNull_IncludesTheNullRow()
        {
            // 'G' matches GB; the "or countryCode eq null" branch must independently pick up the null-member row.
            Assert.AreEqual("GB,none", Match("substringof('G', countryCode) or countryCode eq null"));
        }

        [Test]
        public void BareSubstringofBetweenEqualities_EvaluatesNullRowWithoutThrowing()
        {
            // The null-member row matches none of the three branches, and evaluating substringof against it must not throw.
            Assert.AreEqual("AE,GB,SA", Match("countryCode eq 'AE' or substringof('G', countryCode) or countryCode eq 'SA'"));
        }
    }
}
