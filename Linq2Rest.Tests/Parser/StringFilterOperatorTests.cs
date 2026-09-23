using LinqConvertTools.Tests.Fakes;
using NUnit.Framework;
using System.Linq.Expressions;

namespace LinqConvertTools.Tests.Parser
{
    [TestFixture]
    public class StringFilterOperatorTests
    {
        [TestCase("contains(familyName, 'an')", false, "Jane,Dugane")]
        [TestCase("contains(familyName, 'JAN')", false, "")]
        [TestCase("contains(familyName, 'JAN')", true, "Jane")]
        [TestCase("not contains(familyName, 'an')", false, "One,")]
        [TestCase("contains(familyName, 'an') and givenName eq 'Sarah'", false, "Jane")]
        [TestCase("familyName in ('One', 'Jane')", false, "One,Jane")]
        [TestCase("familyName in ('ONE', 'jane')", false, "")]
        [TestCase("familyName in ('ONE', 'jane')", true, "One,Jane")]
        public void FiltersStrings(string filter, bool ignoreCase, string expected)
        {
            Expression<Func<User, bool>> predicate = new ODataExpressionConverter().Convert<User>(filter, ignoreCase);

            string actual = string.Join(",", CreateUsers().AsQueryable().Where(predicate).Select(u => u.FamilyName));

            Assert.AreEqual(expected, actual, "Failed for " + predicate);
        }

        [Test]
        public void ContainsSkipsNullMembers()
        {
            Expression<Func<User, bool>> predicate = new ODataExpressionConverter().Convert<User>("contains(familyName, 'x')");

            Assert.DoesNotThrow(() => CreateUsers().AsQueryable().Where(predicate).ToList());
        }

        private static List<User> CreateUsers()
        {
            return new()
            {
                new User { GivenName = "User", FamilyName = "One" },
                new User { GivenName = "Sarah", FamilyName = "Jane" },
                new User { GivenName = "Ashley", FamilyName = "Dugane" },
                new User { GivenName = "Nameless", FamilyName = null },
            };
        }
    }
}
