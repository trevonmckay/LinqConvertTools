using LinqConvertTools.Tests.Fakes;
using NUnit.Framework;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace LinqConvertTools.Tests.Parser
{
    [TestFixture]
    public class StringCaseFoldingTests
    {
        private static readonly MethodInfo ToUpper = typeof(string).GetMethod(nameof(string.ToUpper), Type.EmptyTypes)!;
        private static readonly MethodInfo ToLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
        private static readonly MethodInfo ToUpperInvariant = typeof(string).GetMethod(nameof(string.ToUpperInvariant), Type.EmptyTypes)!;
        private static readonly MethodInfo ToLowerInvariant = typeof(string).GetMethod(nameof(string.ToLowerInvariant), Type.EmptyTypes)!;

        private static readonly string[] IgnoreCaseFilters =
        {
            "familyName eq 'jane'",
            "familyName ne 'jane'",
            "familyName in ('one', 'jane')",
            "contains(familyName, 'an')",
            "startswith(familyName, 'ja')",
            "endswith(familyName, 'ne')",
            "indexof(familyName, 'an') eq 1",
            "substringof('an', familyName)",
            "roles/any(r: r eq 'admin')",
        };

        [TestCaseSource(nameof(IgnoreCaseFilters))]
        public void IgnoreCaseUsesToUpperByDefault(string filter)
        {
            Expression<Func<User, bool>> predicate = new ODataExpressionConverter().Convert<User>(filter, ignoreCase: true);

            CollectionAssert.AreEquivalent(new[] { ToUpper }, GetCaseMethods(predicate), "Failed for " + predicate);
        }

        [TestCaseSource(nameof(IgnoreCaseFilters))]
        public void IgnoreCaseUsesToUpperInvariantWhenConfigured(string filter)
        {
            Expression<Func<User, bool>> predicate = new ODataExpressionConverter(StringCaseFolding.Invariant).Convert<User>(filter, ignoreCase: true);

            CollectionAssert.AreEquivalent(new[] { ToUpperInvariant }, GetCaseMethods(predicate), "Failed for " + predicate);
        }

        [TestCase("toupper(familyName) eq 'JANE'", StringCaseFolding.CurrentCulture, nameof(ToUpper))]
        [TestCase("tolower(familyName) eq 'jane'", StringCaseFolding.CurrentCulture, nameof(ToLower))]
        [TestCase("toupper(familyName) eq 'JANE'", StringCaseFolding.Invariant, nameof(ToUpperInvariant))]
        [TestCase("tolower(familyName) eq 'jane'", StringCaseFolding.Invariant, nameof(ToLowerInvariant))]
        public void CaseFunctionsUseConfiguredMethod(string filter, StringCaseFolding caseFolding, string expectedMethod)
        {
            Expression<Func<User, bool>> predicate = new ODataExpressionConverter(caseFolding).Convert<User>(filter);

            CollectionAssert.AreEquivalent(new[] { typeof(string).GetMethod(expectedMethod, Type.EmptyTypes) }, GetCaseMethods(predicate), "Failed for " + predicate);
        }

        [TestCase(StringCaseFolding.CurrentCulture)]
        [TestCase(StringCaseFolding.Invariant)]
        public void IgnoreCaseFiltersInMemory(StringCaseFolding caseFolding)
        {
            var converter = new ODataExpressionConverter(caseFolding);
            var users = new[] { new User { FamilyName = "Jane" }, new User { FamilyName = "One" } };

            string Filter(string filter) => string.Join(",", users.AsQueryable().Where(converter.Convert<User>(filter, ignoreCase: true)).Select(u => u.FamilyName));

            Assert.AreEqual("Jane", Filter("familyName eq 'JANE'"));
            Assert.AreEqual("Jane,One", Filter("familyName in ('jane', 'ONE')"));
            Assert.AreEqual("Jane", Filter("contains(familyName, 'AN')"));
        }

        [Test]
        public void CurrentCultureFoldsInConstantsLikeTheMember()
        {
            // Turkish upper-cases 'i' to dotted 'İ'; the constants must fold the same way as the member or nothing matches.
            WithCulture("tr-TR", () =>
            {
                var users = new[] { new User { FamilyName = "istanbul" } };
                var predicate = new ODataExpressionConverter(StringCaseFolding.CurrentCulture).Convert<User>("familyName in ('istanbul')", ignoreCase: true);

                Assert.AreEqual(1, users.AsQueryable().Count(predicate));
            });
        }

        [Test]
        public void InvariantIgnoresCurrentCulture()
        {
            WithCulture("tr-TR", () =>
            {
                var users = new[] { new User { FamilyName = "istanbul" } };

                Assert.AreEqual(1, users.AsQueryable().Count(new ODataExpressionConverter(StringCaseFolding.Invariant).Convert<User>("familyName eq 'ISTANBUL'", ignoreCase: true)));
                Assert.AreEqual(0, users.AsQueryable().Count(new ODataExpressionConverter(StringCaseFolding.CurrentCulture).Convert<User>("familyName eq 'ISTANBUL'", ignoreCase: true)));
            });
        }

        [Test]
        public void RejectsUndefinedCaseFolding()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ODataExpressionConverter((StringCaseFolding)42));
        }

        private static MethodInfo[] GetCaseMethods(Expression expression)
        {
            var collector = new CaseMethodCollector();
            collector.Visit(expression);
            return collector.Methods.Distinct().ToArray();
        }

        private static void WithCulture(string name, Action action)
        {
            CultureInfo original = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
            try
            {
                action();
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
            }
        }

        private sealed class CaseMethodCollector : ExpressionVisitor
        {
            public List<MethodInfo> Methods { get; } = new();

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method.DeclaringType == typeof(string) && (node.Method.Name.StartsWith("ToUpper", StringComparison.Ordinal) || node.Method.Name.StartsWith("ToLower", StringComparison.Ordinal)))
                {
                    Methods.Add(node.Method);
                }

                return base.VisitMethodCall(node);
            }
        }
    }
}
