using System.Collections.Concurrent;
using System.Linq.Expressions;
using LinqConvertTools.Parser;
using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// One <see cref="SelectExpressionFactory{T}"/> can be shared by concurrent callers.
    /// </summary>
    [TestFixture]
    public class SelectCacheConcurrencyTests
    {
        private const int Threads = 8;

        [Test]
        public void ConcurrentFirstUseOfASharedFactoryReturnsOneSelectorToEveryThread()
        {
            for (int round = 0; round < 50; round++)
            {
                var factory = CreateFactory();
                var barrier = new Barrier(Threads);
                var selectors = new ConcurrentBag<Expression<Func<Row, object>>?>();
                var failures = new ConcurrentBag<Exception>();
                var workers = Enumerable.Range(0, Threads)
                    .Select(_ => new Thread(() =>
                    {
                        barrier.SignalAndWait();
                        try
                        {
                            selectors.Add(factory.Create("A,B"));
                        }
                        catch (Exception ex)
                        {
                            failures.Add(ex);
                        }
                    }))
                    .ToArray();

                foreach (var worker in workers)
                {
                    worker.Start();
                }

                foreach (var worker in workers)
                {
                    worker.Join();
                }

                CollectionAssert.IsEmpty(failures);
                Assert.AreEqual(1, selectors.Distinct().Count(), "every thread gets the cached selector");
            }
        }

        [Test]
        public void ConcurrentUseOfDifferentSelectionsCachesEach()
        {
            var factory = CreateFactory();
            var selections = new[] { "A", "B", "C", "A,B", "A,C", "B,C", "A,B,C", "C,A" };
            var failures = new ConcurrentBag<Exception>();

            Parallel.For(0, 400, i =>
            {
                try
                {
                    factory.Create(selections[i % selections.Length]);
                }
                catch (Exception ex)
                {
                    failures.Add(ex);
                }
            });

            CollectionAssert.IsEmpty(failures);
            Assert.AreSame(factory.Create("A,C"), factory.Create("C, A"));
        }

        [Test]
        public void FailedSelectionIsNotCached()
        {
            var factory = CreateFactory();

            Assert.Throws<ODataParseException>(() => factory.Create("Bogus"));
            Assert.Throws<ODataParseException>(() => factory.Create("Bogus"));
            Assert.IsNotNull(factory.Create("A"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" , ")]
        public void EmptySelectionIsNull(string? selection)
        {
            Assert.IsNull(CreateFactory().Create(selection));
        }

        private static SelectExpressionFactory<Row> CreateFactory()
        {
            var resolver = new MemberNameResolver();
            return new SelectExpressionFactory<Row>(resolver, new RuntimeTypeProvider(resolver));
        }

        public class Row
        {
            public int A { get; set; }

            public string? B { get; set; }

            public double C { get; set; }
        }
    }
}
