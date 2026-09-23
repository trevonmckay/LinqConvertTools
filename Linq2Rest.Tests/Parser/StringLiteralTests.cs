using LinqConvertTools.Tests.Fakes;
using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    [TestFixture]
    public class StringLiteralTests
    {
        private ODataExpressionConverter? _converter;
        private User[]? _users;

        [SetUp]
        public void Setup()
        {
            _converter = new ODataExpressionConverter();
            _users = new[]
            {
                new User { GivenName = "Sarah", FamilyName = "O'Brien" },
                new User { GivenName = "Jane", FamilyName = "Two Words" },
                new User { GivenName = "Empty", FamilyName = string.Empty },
            };
        }

        [TestCase("familyName eq 'O''Brien'", "Sarah")]
        [TestCase("familyName eq 'Two Words'", "Jane")]
        [TestCase("familyName eq ''", "Empty")]
        [TestCase("familyName eq \"Two Words\"", "Jane")]
        [TestCase("familyName in ('O''Brien', 'Two Words')", "Sarah,Jane")]
        [TestCase("contains(familyName, '''')", "Sarah")]
        [TestCase("familyName eq 'O''Brien' and givenName eq 'Sarah'", "Sarah")]
        [TestCase("familyName eq 'O'Brien'", "Sarah")]
        public void ReadsStringLiterals(string filter, string expected)
        {
            ArgumentNullException.ThrowIfNull(_converter);
            ArgumentNullException.ThrowIfNull(_users);

            var predicate = _converter.Convert<User>(filter);

            string actual = string.Join(",", _users.AsQueryable().Where(predicate).Select(u => u.GivenName));

            Assert.AreEqual(expected, actual, "Failed for " + predicate);
        }

        [TestCase("familyName eq 'unterminated")]
        [TestCase("familyName eq '")]
        [TestCase("familyName eq 'mismatched\"")]
        [TestCase("familyName eq \"unterminated")]
        [TestCase("familyName eq 'Two Words")]
        [TestCase("familyName eq 'x' and givenName eq 'y")]
        [TestCase("contains(familyName, 'x)")]
        [TestCase("familyName in ('a', 'b)")]
        public void RejectsUnterminatedStringLiterals(string filter)
        {
            ArgumentNullException.ThrowIfNull(_converter);

            Assert.Throws<FormatException>(() => _converter.Convert<User>(filter));
        }
    }
}
