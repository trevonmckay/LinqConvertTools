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
        private const int SmallStackSize = 1024 * 1024;

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
            string filter = Nest(open, close, 20000);
            var converter = new ODataExpressionConverter();
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

            Assert.IsInstanceOf<InsufficientExecutionStackException>(thrown);
        }

        [Test]
        public void WhenFilterIsNestedModeratelyThenParses()
        {
            string filter = Nest("name eq 'z' or (", ")", 50);
            var converter = new ODataExpressionConverter();

            var predicate = converter.Convert<Item>(filter);

            Assert.AreEqual(1, new[] { new Item { Name = "a" }, new Item { Name = "b" } }.AsQueryable().Count(predicate));
        }

        private static string Nest(string open, string close, int depth)
        {
            return string.Concat(Enumerable.Repeat(open, depth)) + "name eq 'a'" + string.Concat(Enumerable.Repeat(close, depth));
        }

        public class Item
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
