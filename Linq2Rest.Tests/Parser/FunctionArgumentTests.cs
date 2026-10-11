namespace LinqConvertTools.Tests.Parser
{
    using System;
    using System.Linq;
    using NUnit.Framework;

    /// <summary>
    /// Behavioural coverage for filter function arguments: null values reaching a function at any depth, missing
    /// arguments, arguments of the wrong type, and text that follows an <c>in</c> list.
    /// </summary>
    [TestFixture]
    public class FunctionArgumentTests
    {
        private sealed class ChildRow
        {
            public string? Name { get; set; }
        }

        private sealed class Row
        {
            public string Id { get; set; } = string.Empty;

            public string? Name { get; set; }

            public string? Other { get; set; }

            public int Age { get; set; }

            public ChildRow? Child { get; set; }

            public string?[] Tags { get; set; } = Array.Empty<string?>();
        }

        private static readonly Row[] Rows =
        {
            new Row { Id = "a", Name = "Alpha", Other = "lp", Age = 1, Child = new ChildRow { Name = "Kid" }, Tags = new[] { null, "vip" } },
            new Row { Id = "b", Name = "Beta", Other = null, Age = 2, Child = new ChildRow { Name = "Beth" }, Tags = new[] { "gold" } },
            new Row { Id = "c", Name = "Gamma", Other = "Ga", Age = 3, Child = new ChildRow { Name = null }, Tags = new string?[] { null } },
            new Row { Id = "d", Name = "Delta", Other = "ta", Age = 4, Child = new ChildRow { Name = "Bob" } },
            new Row { Id = "n", Name = null, Other = "x", Age = 5, Child = new ChildRow { Name = "Kid" } },
        };

        private static string Match(string filter, bool ignoreCase = false)
        {
            var predicate = new ODataExpressionConverter().Convert<Row>(filter, ignoreCase).Compile();
            return string.Join(",", Rows.Where(predicate).Select(r => r.Id).OrderBy(id => id, StringComparer.Ordinal));
        }

        [Test]
        [TestCase("contains(name, other)", "a,c,d")]
        [TestCase("substringof(other, name)", "a,c,d")]
        [TestCase("startswith(name, other)", "c")]
        [TestCase("endswith(name, other)", "d")]
        [TestCase("indexof(name, other) eq 0", "c")]
        [TestCase("contains(name, null)", "")]
        [TestCase("startswith(name, null)", "")]
        public void NullArgument_IsFalseInsteadOfThrowing(string filter, string expected)
        {
            Assert.AreEqual(expected, Match(filter), "case-sensitive");
            Assert.AreEqual(expected, Match(filter, ignoreCase: true), "ignoreCase");
        }

        [Test]
        [TestCase("length(trim(name)) eq 5", "a,c,d")]
        [TestCase("tolower(trim(name)) eq 'beta'", "b")]
        [TestCase("indexof(tolower(name), 'l') eq 1", "a")]
        [TestCase("indexof(name, 'l') eq 1", "a")]
        [TestCase("length(child/name) eq 3", "a,d,n")]
        [TestCase("tolower(child/name) eq 'kid'", "a,n")]
        [TestCase("'Alpha' eq trim(name)", "a")]
        [TestCase("length(name) add 1 eq 6", "a,c,d")]
        [TestCase("tags/any(t: length(t) eq 3)", "a")]
        public void NullMemberAtAnyDepth_IsFalseInsteadOfThrowing(string filter, string expected)
        {
            Assert.AreEqual(expected, Match(filter), "case-sensitive");
            Assert.AreEqual(expected, Match(filter, ignoreCase: true), "ignoreCase");
        }

        [Test]
        [TestCase("indexof(name) eq 1", "indexof")]
        [TestCase("substring(name) eq 'a'", "substring")]
        [TestCase("contains(name)", "contains")]
        [TestCase("substringof('x')", "substringof")]
        [TestCase("indexof() eq 1", "indexof")]
        [TestCase("length() eq 1", "length")]
        [TestCase("year() eq 1", "year")]
        [TestCase("tolower() eq 'a'", "tolower")]
        [TestCase("trim() eq 'a'", "trim")]
        [TestCase("contains(name, )", "contains")]
        [TestCase("length(name, 1) eq 5", "length")]
        [TestCase("tolower(name, 'x') eq 'alpha'", "tolower")]
        [TestCase("contains(name, 'a', 'b')", "contains")]
        [TestCase("substring(name, 1, 2) eq 'lp'", "substring")]
        [TestCase("LENGTH(name, 1) eq 5", "LENGTH")]
        public void WrongArgumentCount_IsRejectedNamingTheFunction(string filter, string function)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => new ODataExpressionConverter().Convert<Row>(filter, false));
            StringAssert.Contains(function + "()", exception!.Message);
        }

        [Test]
        [TestCase("substringof('x', age)", "substringof", "Int32")]
        [TestCase("contains(age, 'x')", "contains", "Int32")]
        [TestCase("startswith(age, 'x')", "startswith", "Int32")]
        [TestCase("endswith(age, 'x')", "endswith", "Int32")]
        [TestCase("indexof(age, 'x') eq 1", "indexof", "Int32")]
        [TestCase("contains(name, age)", "contains", "Int32")]
        [TestCase("substringof(age, name)", "substringof", "Int32")]
        [TestCase("indexof(name, age) eq 1", "indexof", "Int32")]
        [TestCase("substring(age, 1) eq 'a'", "substring", "Int32")]
        [TestCase("substring(name, name) eq 'a'", "substring", "String")]
        [TestCase("length(age) eq 1", "length", "Int32")]
        [TestCase("tolower(age) eq 'a'", "tolower", "Int32")]
        [TestCase("toupper(age) eq 'A'", "toupper", "Int32")]
        [TestCase("trim(age) eq 'a'", "trim", "Int32")]
        [TestCase("round(name) eq 1", "round", "String")]
        [TestCase("floor(name) eq 1", "floor", "String")]
        [TestCase("ceiling(name) eq 1", "ceiling", "String")]
        [TestCase("year(name) eq 1", "year", "String")]
        public void WrongArgumentType_IsRejectedNamingTheFunctionAndType(string filter, string function, string typeName)
        {
            var exception = Assert.Throws<InvalidOperationException>(() => new ODataExpressionConverter().Convert<Row>(filter, false));
            StringAssert.Contains(function + "()", exception!.Message);
            StringAssert.Contains(typeName, exception.Message);
        }

        [Test]
        [TestCase("name in ('Alpha') eq false", "b,c,d,n")]
        [TestCase("name in ('Alpha') eq true", "a")]
        [TestCase("age in (1, 2) ne true", "c,d,n")]
        [TestCase("name not in ('Alpha') eq false", "a")]
        [TestCase("name in ('Alpha') eq false and age gt 3", "d,n")]
        [TestCase("'vip' in tags eq false", "b,c,d,n")]
        [TestCase("'vip' in tags eq true", "a")]
        [TestCase("tags/any(t: t in ('vip') eq false)", "a,b,c")]
        public void InBindsTighterThanEquality(string filter, string expected)
        {
            Assert.AreEqual(expected, Match(filter));
        }

        [Test]
        [TestCase("name in ('Alpha') eq")]
        [TestCase("name not in ('Alpha') eq")]
        public void ComparisonMissingItsRightSideAfterInList_IsRejected(string filter)
        {
            Assert.Throws<InvalidOperationException>(() => new ODataExpressionConverter().Convert<Row>(filter, false));
        }
    }
}
