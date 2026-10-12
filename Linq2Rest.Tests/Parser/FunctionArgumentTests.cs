namespace LinqConvertTools.Tests.Parser
{
    using System;
    using System.Linq;
    using NUnit.Framework;

    /// <summary>
    /// Behavioural coverage for filter function arguments: null values reaching a function at any depth, missing
    /// arguments, arguments of the wrong type, and text that follows an <c>in</c> list. A function of null is null, as
    /// OData specifies: <c>eq</c> and <c>ne</c> treat it as a value, <c>gt</c> and the other ordering comparisons are
    /// false for it, and a boolean function of null is unknown, which <c>not</c>, <c>and</c> and <c>or</c> propagate.
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
        [TestCase("not startswith(name, other)", "a,d")]
        [TestCase("startswith(name, other) eq false", "a,d")]
        [TestCase("startswith(name, other) ne true", "a,b,d,n")]
        [TestCase("startswith(name, other) eq null", "b,n")]
        [TestCase("startswith(name, other) ne false", "b,c,n")]
        [TestCase("startswith(name, other) or age eq 2", "b,c")]
        [TestCase("not (startswith(name, other) and age eq 5)", "a,b,c,d")]
        [TestCase("not (startswith(name, other) or age eq 1)", "d")]
        [TestCase("startswith(name, other) eq startswith(other, name)", "a,b,d,n")]
        public void NullArgument_IsUnknownInsteadOfThrowing(string filter, string expected)
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
        [TestCase("length(trim(name)) ne 5", "b,n")]
        [TestCase("tolower(trim(name)) eq null", "n")]
        [TestCase("trim(name) ne null", "a,b,c,d")]
        [TestCase("length(child/name) ne 3", "b,c")]
        [TestCase("length(child/name) gt 3", "b")]
        [TestCase("not (length(child/name) gt 3)", "a,c,d,n")]
        [TestCase("'Alpha' ne trim(name)", "b,c,d,n")]
        [TestCase("length(name) add 1 ne 6", "b,n")]
        [TestCase("indexof(name, other) ne 0", "a,b,d,n")]
        [TestCase("tolower(child/name) eq tolower(child/name)", "a,b,c,d,n")]
        [TestCase("substring(name, length(other)) eq 'pha'", "a")]
        [TestCase("substring(name, length(other)) ne 'pha'", "b,c,d,n")]
        [TestCase("tags/any(t: length(t) ne 3)", "a,b,c")]
        [TestCase("tags/all(t: length(t) eq 3)", "d,n")]
        public void NullMemberAtAnyDepth_IsNullInsteadOfThrowing(string filter, string expected)
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
            var exception = ParseAssert.Throws<InvalidOperationException>(() => new ODataExpressionConverter().Convert<Row>(filter, false));
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
            var exception = ParseAssert.Throws<InvalidOperationException>(() => new ODataExpressionConverter().Convert<Row>(filter, false));
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
        [TestCase("true eq name in ('Alpha')", "a")]
        [TestCase("false eq name in ('Alpha')", "b,c,d,n")]
        [TestCase("name in ('Alpha') not in (true)", "b,c,d,n")]
        [TestCase("name in ('Alpha') in (false)", "b,c,d,n")]
        [TestCase("name in ('Alpha') in (true) eq false", "b,c,d,n")]
        [TestCase("trim(name) in ('Alpha')", "a")]
        [TestCase("trim(name) in ('Alpha', null)", "a,n")]
        [TestCase("trim(name) not in ('Alpha')", "b,c,d,n")]
        [TestCase("contains(name, 'l') in (false)", "b,c")]
        public void InBindsTighterThanEquality(string filter, string expected)
        {
            Assert.AreEqual(expected, Match(filter));
        }

        [Test]
        [TestCase("name in ('Alpha') eq")]
        [TestCase("name not in ('Alpha') eq")]
        public void ComparisonMissingItsRightSideAfterInList_IsRejected(string filter)
        {
            ParseAssert.Throws<InvalidOperationException>(() => new ODataExpressionConverter().Convert<Row>(filter, false));
        }
    }
}
