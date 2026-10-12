using LinqConvertTools.Parser;
using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// A sort path resolves every segment, as a filter path does.
    /// </summary>
    [TestFixture]
    public class SortPathTests
    {
        [TestCase("IntValue", "x => x.IntValue")]
        [TestCase("Child/Attributes desc", "x => x.Child.Attributes")]
        public void ResolvesMemberPath(string orderBy, string expected)
        {
            var factory = new SortExpressionFactory(new MemberNameResolver());

            var description = factory.Create<FakeItem>(orderBy).Single();

            Assert.AreEqual(expected, description.KeySelector.ToString());
        }

        [TestCase("Bogus")]
        [TestCase("Bogus/IntValue")]
        [TestCase("Child/Bogus")]
        public void RejectsPathThatDoesNotResolve(string orderBy)
        {
            var factory = new SortExpressionFactory(new MemberNameResolver());

            Assert.Throws<FormatException>(() => factory.Create<FakeItem>(orderBy).ToArray());
        }
    }
}
