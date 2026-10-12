using System.Collections.Specialized;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using LinqConvertTools.Parser;
using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// Every failure caused by query text is reported as an <see cref="ODataParseException"/> that names the query
    /// option and keeps the original failure, while failures that indicate a defect pass through unwrapped.
    /// </summary>
    [TestFixture]
    public class ODataParseExceptionTests
    {
        [Test]
        public void FilterFailureNamesTheQueryOptionAndText()
        {
            const string filter = "IntValue eq 'nothing'";

            var exception = ParseAssert.Throws<InvalidOperationException>(() => new ODataExpressionConverter().Convert<FakeItem>(filter));

            Assert.AreEqual("$filter", exception.QueryOption);
            Assert.AreEqual(filter, exception.Text);
            StringAssert.StartsWith("The $filter query option could not be parsed. ", exception.Message);
            StringAssert.EndsWith(exception.InnerException!.Message, exception.Message);
        }

        [Test]
        public void FilterFailureWithFormatProviderIsWrapped()
        {
            var factory = new FilterExpressionFactory(new MemberNameResolver(), Enumerable.Empty<LinqConvertTools.Parser.Readers.IValueExpressionFactory>());

            var exception = ParseAssert.Throws<FormatException>(() => factory.Create<FakeItem>("DateValue eq 123", System.Globalization.CultureInfo.InvariantCulture, true));

            Assert.AreEqual("$filter", exception.QueryOption);
        }

        [Test]
        public void FilterNestedTooDeeplyIsTooComplex()
        {
            var converter = new ODataExpressionConverter { MaxDepth = 3 };

            var exception = ParseAssert.ThrowsTooDeep(() => converter.Convert<FakeItem>("not not not not IntValue eq 1"));

            Assert.AreEqual("$filter", exception.QueryOption);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void BlankFilterMatchesEverything(string? filter)
        {
            var predicate = new ODataExpressionConverter().Convert<FakeItem>(filter!);

            Assert.IsTrue(predicate.Compile()(new FakeItem()));
        }

        [Test]
        public void OrderByFailureIsThrownWhenCreatedNotWhenEnumerated()
        {
            var factory = new SortExpressionFactory(new MemberNameResolver());

            var exception = ParseAssert.Throws<FormatException>(() => factory.Create<FakeItem>("IntValue, Bogus desc"));

            Assert.AreEqual("$orderby", exception.QueryOption);
            Assert.AreEqual("IntValue, Bogus desc", exception.Text);
        }

        [TestCase("IntValue,", new[] { "x => x.IntValue" })]
        [TestCase(" , IntValue", new[] { "x => x.IntValue" })]
        [TestCase("IntValue,,Child/Attributes desc", new[] { "x => x.IntValue", "x => x.Child.Attributes" })]
        [TestCase("IntValue, ,DoubleValue", new[] { "x => x.IntValue", "x => x.DoubleValue" })]
        public void OrderBySkipsBlankEntries(string orderBy, string[] expected)
        {
            var factory = new SortExpressionFactory(new MemberNameResolver());

            var descriptions = factory.Create<FakeItem>(orderBy);

            CollectionAssert.AreEqual(expected, descriptions.Select(d => d.KeySelector.ToString()));
        }

        [Test]
        public void OrderByOfOnlyBlankEntriesIsEmpty()
        {
            var factory = new SortExpressionFactory(new MemberNameResolver());

            CollectionAssert.IsEmpty(factory.Create<FakeItem>(" , ,"));
        }

        [Test]
        public void SelectOfUnknownMemberNamesTheMember()
        {
            var factory = CreateSelectFactory();

            var exception = ParseAssert.Throws<FormatException>(() => factory.Create("DoubleValue,Bogus"));

            Assert.AreEqual("$select", exception.QueryOption);
            Assert.AreEqual("DoubleValue,Bogus", exception.Text);
            StringAssert.Contains("Bogus is not recognized as a valid property", exception.Message);
        }

        [Test]
        public void SelectNamingAMemberTwiceIsInvalid()
        {
            var factory = CreateSelectFactory();

            var exception = ParseAssert.Throws<ArgumentException>(() => factory.Create("DoubleValue,DoubleValue"));

            Assert.AreEqual("$select", exception.QueryOption);
        }

        [Test]
        public void SelectStillWorksAfterAFailedSelect()
        {
            var factory = CreateSelectFactory();
            ParseAssert.Throws<FormatException>(() => factory.Create("Bogus"));

            var selector = factory.Create("DoubleValue");

            Assert.IsNotNull(selector);
        }

        [TestCase("$skip", "x", typeof(FormatException))]
        [TestCase("$top", "'123'", typeof(FormatException))]
        [TestCase("$top", "99999999999", typeof(OverflowException))]
        public void CountFailureNamesTheQueryOption(string queryOption, string value, Type innerType)
        {
            var parameters = new NameValueCollection { { queryOption, value } };

            var exception = ParseAssert.Throws(innerType, () => new ParameterParser<FakeItem>().Parse(parameters));

            Assert.AreEqual(queryOption, exception.QueryOption);
            Assert.AreEqual(value, exception.Text);
        }

        [TestCase("$filter", "IntValue eq 'nothing'")]
        [TestCase("$orderby", "Bogus")]
        [TestCase("$select", "Bogus")]
        public void ParameterParserNamesTheFailingQueryOption(string queryOption, string value)
        {
            var parameters = new NameValueCollection { { queryOption, value } };

            var exception = Assert.Throws<ODataParseException>(() => new ParameterParser<FakeItem>().Parse(parameters))!;

            Assert.AreEqual(queryOption, exception.QueryOption);
            Assert.AreEqual(value, exception.Text);
            Assert.AreEqual(ODataParseErrorKind.Invalid, exception.Kind);
        }

        [Test]
        public void DefectInACustomResolverIsNotWrapped()
        {
            var converter = new ODataExpressionConverter(
                Array.Empty<LinqConvertTools.Provider.Writers.IValueWriter>(),
                Array.Empty<LinqConvertTools.Parser.Readers.IValueExpressionFactory>(),
                new FaultyResolver());

            Assert.Throws<NullReferenceException>(() => converter.Convert<FakeItem>("IntValue eq 1"));
        }

        [TestCase(typeof(InsufficientExecutionStackException))]
        [TestCase(typeof(RegexMatchTimeoutException))]
        [TestCase(typeof(MaxDepthExceededException))]
        public void LimitFailureIsTooComplex(Type failureType)
        {
            var failure = (Exception)Activator.CreateInstance(failureType)!;

            var exception = Assert.Throws<ODataParseException>(() => Guard(failure))!;

            Assert.AreSame(failure, exception.InnerException);
            Assert.AreEqual(ODataParseErrorKind.TooComplex, exception.Kind);
        }

        [TestCase(typeof(InvalidOperationException))]
        [TestCase(typeof(FormatException))]
        [TestCase(typeof(ArgumentException))]
        [TestCase(typeof(KeyNotFoundException))]
        [TestCase(typeof(OverflowException))]
        [TestCase(typeof(NotSupportedException))]
        public void InputFailureIsInvalid(Type failureType)
        {
            var failure = (Exception)Activator.CreateInstance(failureType)!;

            var exception = Assert.Throws<ODataParseException>(() => Guard(failure))!;

            Assert.AreSame(failure, exception.InnerException);
            Assert.AreEqual(ODataParseErrorKind.Invalid, exception.Kind);
        }

        [TestCase(typeof(NullReferenceException))]
        [TestCase(typeof(IndexOutOfRangeException))]
        [TestCase(typeof(InvalidCastException))]
        [TestCase(typeof(ArgumentNullException))]
        [TestCase(typeof(ArgumentOutOfRangeException))]
        [TestCase(typeof(NotImplementedException))]
        public void DefectIsNotWrapped(Type failureType)
        {
            var failure = (Exception)Activator.CreateInstance(failureType)!;

            var thrown = Assert.Catch(() => Guard(failure));

            Assert.AreSame(failure, thrown);
        }

        [Test]
        public void ObjectDisposedIsNotWrapped()
        {
            var failure = new ObjectDisposedException("reader");

            var thrown = Assert.Catch(() => Guard(failure));

            Assert.AreSame(failure, thrown);
        }

        [Test]
        public void ParseExceptionIsNotWrappedAgain()
        {
            var failure = new ODataParseException("inner", "$filter", "x", ODataParseErrorKind.Invalid, null);

            var thrown = Assert.Catch(() => Guard(failure));

            Assert.AreSame(failure, thrown);
        }

        [Test]
        public void SerializationKeepsQueryOptionTextAndKind()
        {
            var original = new ODataParseException("message", "$orderby", "Bogus", ODataParseErrorKind.TooComplex, new FormatException("inner"));
            var context = new StreamingContext(StreamingContextStates.All);
            var info = new SerializationInfo(typeof(ODataParseException), new FormatterConverter());

#pragma warning disable SYSLIB0051
            original.GetObjectData(info, context);
#pragma warning restore SYSLIB0051
            var copy = new DeserializedParseException(info, context);

            Assert.AreEqual("message", copy.Message);
            Assert.AreEqual("$orderby", copy.QueryOption);
            Assert.AreEqual("Bogus", copy.Text);
            Assert.AreEqual(ODataParseErrorKind.TooComplex, copy.Kind);
            Assert.IsInstanceOf<FormatException>(copy.InnerException);
        }

        private static void Guard(Exception failure)
        {
            ParseFailure.Guard<object>("$filter", "text", () => throw failure);
        }

        private static SelectExpressionFactory<FakeItem> CreateSelectFactory()
        {
            var resolver = new MemberNameResolver();
            return new SelectExpressionFactory<FakeItem>(resolver, new RuntimeTypeProvider(resolver));
        }

        private sealed class FaultyResolver : IMemberNameResolver
        {
            public string ResolveName(MemberInfo member)
            {
                throw new NullReferenceException();
            }

            public MemberInfo? ResolveAlias(Type type, string alias)
            {
                throw new NullReferenceException();
            }
        }

        private sealed class DeserializedParseException : ODataParseException
        {
            public DeserializedParseException(SerializationInfo info, StreamingContext context)
                : base(info, context)
            {
            }
        }
    }
}
