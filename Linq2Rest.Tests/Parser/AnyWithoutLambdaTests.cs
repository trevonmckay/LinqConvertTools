using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// OData allows <c>any()</c> without a lambda to test whether a collection has elements; <c>all()</c> always requires a lambda.
    /// </summary>
    [TestFixture]
    public class AnyWithoutLambdaTests
    {
        private ODataExpressionConverter _converter;
        private Team[] _teams;

        [SetUp]
        public void Setup()
        {
            _converter = new ODataExpressionConverter();
            _teams = new[]
            {
                new Team { Name = "Staffed", Members = { new Member { Roles = { "admin" } }, new Member() } },
                new Team { Name = "Unassigned", Members = { new Member() } },
                new Team { Name = "Empty" },
            };
        }

        [TestCase("members/any()", false, "Staffed,Unassigned")]
        [TestCase("members/any( )", false, "Staffed,Unassigned")]
        [TestCase("not members/any()", false, "Empty")]
        [TestCase("members/any() and name ne 'Staffed'", false, "Unassigned")]
        [TestCase("members/any(m: m/roles/any())", false, "Staffed")]
        [TestCase("members/all(m: m/roles/any())", false, "Empty")]
        [TestCase("members/any() and name eq 'unassigned'", true, "Unassigned")]
        public void FiltersByNonEmptyCollection(string filter, bool ignoreCase, string expected)
        {
            var predicate = _converter.Convert<Team>(filter, ignoreCase);

            string actual = string.Join(",", _teams.AsQueryable().Where(predicate).Select(t => t.Name));

            Assert.AreEqual(expected, actual, "Failed for " + predicate);
        }

        [Test]
        public void CreatesEnumerableAnyWithoutPredicate()
        {
            var predicate = _converter.Convert<Team>("members/any()");

            Assert.AreEqual("x => x.Members.Any()", predicate.ToString());
        }

        [TestCase("members/all()")]
        [TestCase("members/any(m/roles/any())")]
        [TestCase("members/all(m/roles/any())")]
        public void RejectsMissingLambda(string filter)
        {
            Assert.Throws<InvalidOperationException>(() => _converter.Convert<Team>(filter));
        }

        public class Team
        {
            public string Name { get; set; } = string.Empty;

            public List<Member> Members { get; set; } = new();
        }

        public class Member
        {
            public List<string> Roles { get; set; } = new();
        }
    }
}
