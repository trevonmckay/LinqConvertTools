using LinqConvertTools.Parser;
using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// Parsing stays bounded in stack and time: a filter beyond the parser's limits fails with a catchable exception.
    /// </summary>
    [TestFixture]
    public class ParserLimitTests
    {
        private const int SmallStackSize = 256 * 1024;
        private const int BeyondSmallStack = 3000;

        [Test]
        public void WhenAnyAllPathIsLongWithoutLambdaThenTokenizesInLinearTime()
        {
            string filter = string.Concat(Enumerable.Repeat("a/", 2000)) + "any(";

            TokenSet? tokens = null;
            Assert.DoesNotThrow(() => tokens = filter.GetAnyAllFunctionTokens());

            Assert.IsNull(tokens);
        }

        [Test]
        public void WhenAnyAllPathHasManySegmentsThenTokenizesPathAndFunction()
        {
            string path = string.Join("/", Enumerable.Repeat("a", 60));

            var tokens = (path + "/any(x: x eq 1)").GetAnyAllFunctionTokens();

            Assert.IsNotNull(tokens);
            Assert.AreEqual(path, tokens!.Left);
            Assert.AreEqual("any", tokens.Operation);
        }

        [TestCase("name eq 'z' or (", ")")]
        [TestCase("not ", "")]
        public void WhenFilterIsNestedBeyondTheStackThenThrowsInsufficientExecutionStack(string open, string close)
        {
            string filter = Nest(open, close, BeyondSmallStack);
            var converter = new ODataExpressionConverter { MaxDepth = int.MaxValue };

            Exception? thrown = ConvertOnSmallStack(converter, filter);

            Assert.IsInstanceOf<InsufficientExecutionStackException>(thrown);
        }

        [Test]
        public void WhenLambdaConditionChainExceedsTheStackThenThrowsInsufficientExecutionStack()
        {
            string filter = "tags/any(t: " + Chain("t eq 'z' or ", "t eq 'a'", BeyondSmallStack) + ")";
            var converter = new ODataExpressionConverter { MaxDepth = int.MaxValue };

            Exception? thrown = ConvertOnSmallStack(converter, filter);

            Assert.IsInstanceOf<InsufficientExecutionStackException>(thrown);
        }

        [Test]
        public void WhenFilterIsNestedModeratelyThenParses()
        {
            string filter = Nest("name eq 'z' or (", ")", 50);
            var converter = new ODataExpressionConverter();

            var predicate = converter.Convert<Item>(filter);

            Assert.AreEqual(1, Items().Count(predicate.Compile()));
        }

        [TestCase("name eq 'z' or (", ")")]
        [TestCase("not ", "")]
        public void WhenFilterNestsDeeperThanMaxDepthThenThrowsInvalidOperation(string open, string close)
        {
            string filter = Nest(open, close, FilterExpressionFactory.DefaultMaxDepth + 1);
            var converter = new ODataExpressionConverter();

            Assert.Throws<InvalidOperationException>(() => converter.Convert<Item>(filter));
        }

        [Test]
        public void WhenConditionChainIsLongerThanMaxDepthThenThrowsInvalidOperation()
        {
            string filter = Chain("name eq 'z' or ", "name eq 'a'", FilterExpressionFactory.DefaultMaxDepth);
            var converter = new ODataExpressionConverter();

            Assert.Throws<InvalidOperationException>(() => converter.Convert<Item>(filter));
        }

        [Test]
        public void WhenConditionChainFitsMaxDepthThenParses()
        {
            string filter = Chain("name eq 'z' or ", "name eq 'a'", FilterExpressionFactory.DefaultMaxDepth / 2);
            var converter = new ODataExpressionConverter();

            var predicate = converter.Convert<Item>(filter);

            Assert.AreEqual(1, Items().Count(predicate.Compile()));
        }

        [Test]
        public void WhenMaxDepthIsLoweredThenRejectsShallowerFilters()
        {
            var converter = new ODataExpressionConverter { MaxDepth = 10 };

            var shallow = converter.Convert<Item>(Nest("not ", "", 2));

            Assert.IsNotNull(shallow);
            Assert.Throws<InvalidOperationException>(() => converter.Convert<Item>(Nest("not ", "", 20)));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void WhenMaxDepthIsLessThanOneThenThrowsArgumentOutOfRange(int maxDepth)
        {
            var converter = new ODataExpressionConverter();

            Assert.Throws<ArgumentOutOfRangeException>(() => converter.MaxDepth = maxDepth);
        }

        private static string Nest(string open, string close, int depth)
        {
            return string.Concat(Enumerable.Repeat(open, depth)) + "name eq 'a'" + string.Concat(Enumerable.Repeat(close, depth));
        }

        private static string Chain(string clause, string last, int count)
        {
            return string.Concat(Enumerable.Repeat(clause, count)) + last;
        }

        private static Item[] Items()
        {
            return new[] { new Item { Name = "a" }, new Item { Name = "b" } };
        }

        private static Exception? ConvertOnSmallStack(ODataExpressionConverter converter, string filter)
        {
            Exception? thrown = null;
            var thread = new Thread(
                () =>
                {
                    try
                    {
                        converter.Convert<Item>(filter);
                    }
                    catch (Exception ex)
                    {
                        thrown = ex;
                    }
                },
                SmallStackSize);

            thread.Start();
            thread.Join();

            return thrown;
        }

        public class Item
        {
            public string Name { get; set; } = string.Empty;

            public List<string> Tags { get; set; } = new();
        }
    }
}
