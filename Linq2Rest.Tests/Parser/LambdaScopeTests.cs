using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// A lambda variable of <c>any</c>/<c>all</c> is in scope only inside its own lambda, an inner variable of the same
    /// name hides an outer one, and an unqualified member inside a lambda is a member of the filtered item.
    /// </summary>
    [TestFixture]
    public class LambdaScopeTests
    {
        private ODataExpressionConverter? _converter;
        private Team[]? _teams;

        [SetUp]
        public void Setup()
        {
            _converter = new ODataExpressionConverter();
            _teams = new[]
            {
                new Team
                {
                    Name = "Alpha",
                    Size = 1,
                    Tags = { "vip" },
                    Members = { new Member { Name = "vip", Tags = { "vip" } }, new Member { Name = "x" } },
                },
                new Team
                {
                    Name = "Beta",
                    Size = 5,
                    Tags = { "gold" },
                    Members = { new Member { Name = "gold", Tags = { "x" } } },
                },
                new Team { Name = "Empty", Size = 9 },
            };
        }

        [TestCase("tags/any(t: size gt 0)", "Alpha,Beta")]
        [TestCase("tags/any(t: t eq 'vip') and tags/any(t: size gt 0)", "Alpha")]
        [TestCase("tags/any(t: size gt 0) and tags/any(t: t eq 'vip')", "Alpha")]
        [TestCase("tags/any(t: true) or tags/any(u: true)", "Alpha,Beta")]
        [TestCase("tags/all(t: false) or tags/any(u: u eq 'gold')", "Beta,Empty")]
        [TestCase("members/any(m: m/name eq 'x') and members/any(n: true)", "Alpha")]
        [TestCase("tags/any(t: t eq 'gold') or members/any(t: t/name eq 'vip')", "Alpha,Beta")]
        [TestCase("members/any(m: m/name eq 'gold') and tags/any(m: m eq 'gold')", "Beta")]
        [TestCase("members/any(m: m/tags/any(t: size gt 3))", "Beta")]
        [TestCase("members/any(m: tags/any(t: true))", "Alpha,Beta")]
        [TestCase("members/any(m: tags/any(t: t eq m/name))", "Alpha,Beta")]
        [TestCase("members/any(m: m/tags/any(t: t eq m/name))", "Alpha")]
        [TestCase("members/all(m: m/tags/any(t: t eq m/name))", "Empty")]
        [TestCase("members/any(m: m/tags/any(m: m eq 'x'))", "Beta")]
        [TestCase("members/any(m: size gt 3 and m/tags/any(t: t eq 'x'))", "Beta")]
        public void LambdaVariablesAreScopedToTheirLambda(string filter, string expected)
        {
            ArgumentNullException.ThrowIfNull(_converter);
            ArgumentNullException.ThrowIfNull(_teams);

            var predicate = _converter.Convert<Team>(filter);

            string actual = string.Join(",", _teams.AsQueryable().Where(predicate).Select(t => t.Name));

            Assert.AreEqual(expected, actual, "Failed for " + predicate);
        }

        [TestCase("members/any(m: tags/any(t: t eq m/name))", "x => x.Members.Any(m => x.Tags.Any(t => (t == m.Name)))")]
        [TestCase("tags/any(t: true) or tags/any(u: true)", "x => (x.Tags.Any(t => True) OrElse x.Tags.Any(u => True))")]
        public void BindsEachLambdaToItsOwnVariable(string filter, string expected)
        {
            ArgumentNullException.ThrowIfNull(_converter);

            Assert.AreEqual(expected, _converter.Convert<Team>(filter).ToString());
        }

        [TestCase("tags/any(t: t eq 'vip') and t eq 'vip'")]
        [TestCase("members/any(m: m/tags/any(t: true) and t eq 'x')")]
        public void RejectsLambdaVariableOutsideItsLambda(string filter)
        {
            ArgumentNullException.ThrowIfNull(_converter);

            Assert.Throws<InvalidOperationException>(() => _converter.Convert<Team>(filter));
        }

        public class Team
        {
            public string Name { get; set; } = string.Empty;

            public int Size { get; set; }

            public List<string> Tags { get; set; } = new();

            public List<Member> Members { get; set; } = new();
        }

        public class Member
        {
            public string Name { get; set; } = string.Empty;

            public List<string> Tags { get; set; } = new();
        }
    }
}
