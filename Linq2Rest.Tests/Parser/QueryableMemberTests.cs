using LinqConvertTools.Parser;
using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// The query options read only members that hold a readable value.
    /// </summary>
    [TestFixture]
    public class QueryableMemberTests
    {
        [TestCase("Item")]
        [TestCase("WriteOnly")]
        public void SelectOfAMemberWithoutAReadableValueIsNotRecognized(string selection)
        {
            var exception = ParseAssert.Throws<FormatException>(() => CreateSelectFactory<Row>().Create(selection));

            StringAssert.Contains(selection + " is not recognized as a valid property", exception.Message);
        }

        [TestCase("Item eq 1")]
        [TestCase("WriteOnly eq 1")]
        public void FilterOnAMemberWithoutAReadableValueIsInvalid(string filter)
        {
            var exception = Assert.Throws<ODataParseException>(() => new ODataExpressionConverter().Convert<Row>(filter))!;

            Assert.AreEqual(ODataParseErrorKind.Invalid, exception.Kind);
        }

        [TestCase("_secret")]
        [TestCase("Hidden")]
        [TestCase("PrivateGetter")]
        [TestCase("ProtectedValue")]
        public void SelectOfAnUnmarkedNonPublicMemberIsNotRecognized(string selection)
        {
            ParseAssert.Throws<FormatException>(() => CreateSelectFactory<Account>().Create(selection));
        }

        [TestCase("_secret eq 'x'")]
        [TestCase("Hidden eq 'x'")]
        [TestCase("PrivateGetter eq 'x'")]
        [TestCase("ProtectedValue eq 1")]
        [TestCase("_SECRET eq 'x'")]
        public void FilterOnAnUnmarkedNonPublicMemberIsInvalid(string filter)
        {
            var exception = ParseAssert.Throws<InvalidOperationException>(() => new ODataExpressionConverter().Convert<Account>(filter));

            StringAssert.Contains("Could not create expression from: " + filter.Split(' ')[0], exception.Message);
        }

        [TestCase("_secret")]
        [TestCase("Hidden desc")]
        public void OrderByOfAnUnmarkedNonPublicMemberIsNotRecognized(string orderBy)
        {
            ParseAssert.Throws<FormatException>(() => new SortExpressionFactory(new MemberNameResolver()).Create<Account>(orderBy));
        }

        [Test]
        public void MarkedNonPublicMembersRemainQueryable()
        {
            var accounts = new[] { new Account("a", "s1", 1), new Account("b", "s2", 2) };

            var filter = new ODataExpressionConverter().Convert<Account>("Code eq 's2' and Level eq 2");
            var sort = new SortExpressionFactory(new MemberNameResolver()).Create<Account>("Code desc").Single();
            var selector = CreateSelectFactory<Account>().Create("Code,Level");

            Assert.AreEqual(new[] { "b" }, accounts.Where(filter.Compile()).Select(x => x.Name).ToArray());
            Assert.IsNotNull(sort);
            Assert.IsNotNull(selector);
        }

        [Test]
        public void OverrideOfAMarkedNonPublicPropertyRemainsQueryable()
        {
            var filter = new ODataExpressionConverter().Convert<DerivedAccount>("Level eq 2");
            var sort = new SortExpressionFactory(new MemberNameResolver()).Create<DerivedAccount>("Level desc").Single();
            var selector = CreateSelectFactory<DerivedAccount>().Create("Level");

            Assert.IsTrue(filter.Compile()(new DerivedAccount(2)));
            Assert.IsNotNull(sort);
            Assert.IsNotNull(selector);
        }

        [Test]
        public void MarkedFieldIsReadWhenItsFrontingPropertyIsWriteOnly()
        {
            var filter = new ODataExpressionConverter().Convert<Credential>("Token eq 'abc'");
            var credential = new Credential { Token = "abc" };

            Assert.IsTrue(filter.Compile()(credential));
        }

        [Test]
        public void PublicMembersRemainQueryable()
        {
            var filter = new ODataExpressionConverter().Convert<Account>("Name eq 'a'");

            Assert.IsTrue(filter.Compile()(new Account("a", "s", 0)));
        }

        [Test]
        public void SelectOfReadableMembersStillWorks()
        {
            var selector = CreateSelectFactory<Row>().Create("Id,Name");

            Assert.IsNotNull(selector);
        }

        private static SelectExpressionFactory<T> CreateSelectFactory<T>()
        {
            var resolver = new MemberNameResolver();
            return new SelectExpressionFactory<T>(resolver, new RuntimeTypeProvider(resolver));
        }

        public class Account
        {
            private string _secret;

            [System.Runtime.Serialization.DataMember(Name = "Code")]
            private string _code;

            public Account()
                : this(string.Empty, string.Empty, 0)
            {
            }

            public Account(string name, string code, int level)
            {
                Name = name;
                _code = code;
                Level = level;
                _secret = "hash-" + name;
                Hidden = _secret;
                PrivateGetter = _secret;
            }

            public string Name { get; set; }

            [System.Runtime.Serialization.DataMember]
            internal int Level { get; set; }

            public string PrivateGetter { private get; set; }

            protected int ProtectedValue { get; set; }

            private string Hidden { get; set; }

            public override string ToString()
            {
                return _secret + _code + Hidden + PrivateGetter + ProtectedValue;
            }
        }

        public class BaseAccount
        {
            [System.Runtime.Serialization.DataMember]
            protected internal virtual int Level { get; set; }
        }

        public class DerivedAccount : BaseAccount
        {
            public DerivedAccount()
            {
            }

            public DerivedAccount(int level)
            {
                Level = level;
            }

            protected internal override int Level { get; set; }
        }

        public class Credential
        {
            [System.Runtime.Serialization.DataMember(Name = "Token")]
            private string? _token;

            public string? Token
            {
                set => _token = value;
            }

            public override string? ToString()
            {
                return _token;
            }
        }

        public class Row
        {
            public int Id { get; set; }

            public string? Name { get; set; }

            public int WriteOnly
            {
                set => Id = value;
            }

            public int this[int index] => index;
        }
    }
}
