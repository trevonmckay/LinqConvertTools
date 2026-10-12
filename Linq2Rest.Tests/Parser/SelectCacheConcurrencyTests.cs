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
                var factory = CreateUnsharedFactory(new CountingTypeProvider());
                using var barrier = new Barrier(Threads);
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
        public void ConcurrentFirstUseBuildsTheSelectorOnce()
        {
            for (int round = 0; round < 50; round++)
            {
                var provider = new CountingTypeProvider();
                var factory = CreateUnsharedFactory(provider);
                using var barrier = new Barrier(Threads);
                var workers = Enumerable.Range(0, Threads)
                    .Select(_ => new Thread(() =>
                    {
                        barrier.SignalAndWait();
                        factory.Create("A,C");
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

                Assert.AreEqual(1, provider.Calls, "the runtime type is requested once per selection");
            }
        }

        [Test]
        public void FactoriesWithTheBuiltInServicesShareSelectors()
        {
            Assert.AreSame(CreateFactory().Create("B,C"), CreateFactory().Create("C,B"));
        }

        [Test]
        public void ParsersWithTheBuiltInServicesShareSelectors()
        {
            var first = new ParameterParser<Row>().Parse(new System.Collections.Specialized.NameValueCollection { { "$select", "A,B" } });
            var second = new ParameterParser<Row>().Parse(new System.Collections.Specialized.NameValueCollection { { "$select", "B,A" } });

            Assert.AreSame(SelectorOf(first), SelectorOf(second));
        }

        [Test]
        public void FactoriesWithACustomResolverDoNotShareSelectors()
        {
            var shared = CreateFactory().Create("A");
            var custom = CreateUnsharedFactory(new RuntimeTypeProvider(new CustomResolver())).Create("A");

            Assert.AreNotSame(shared, custom);
        }

        [Test]
        public void SelectionsBeyondTheRuntimeTypeLimitAreTooComplex()
        {
            var resolver = new MemberNameResolver();
            var factory = new SelectExpressionFactory<LimitRow>(resolver, new RuntimeTypeProvider(resolver));
            var built = factory.Create("X");
            var original = RuntimeTypeProvider.MaxRuntimeTypes;
            try
            {
                RuntimeTypeProvider.MaxRuntimeTypes = RuntimeTypeProvider.BuiltTypeCount;

                var exception = Assert.Throws<ODataParseException>(() => factory.Create("X,Y"))!;

                Assert.AreEqual(ODataParseErrorKind.TooComplex, exception.Kind);
                Assert.AreEqual(StringConstants.SelectParameter, exception.QueryOption);
                Assert.AreSame(built, factory.Create("X"), "selections built before the limit keep working");
            }
            finally
            {
                RuntimeTypeProvider.MaxRuntimeTypes = original;
            }

            Assert.IsNotNull(factory.Create("X,Y"), "a selection rejected at the limit is not cached as a failure");
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void RuntimeTypeLimitMustBePositive(int limit)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeTypeProvider.MaxRuntimeTypes = limit);
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
            var cached = selections.ToDictionary(x => x, factory.Create);
            foreach (var selection in selections)
            {
                Assert.IsNotNull(cached[selection], selection);
                Assert.AreSame(cached[selection], factory.Create(selection), selection);
            }

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

        private static SelectExpressionFactory<Row> CreateUnsharedFactory(IRuntimeTypeProvider provider)
        {
            return new SelectExpressionFactory<Row>(new CustomResolver(), provider);
        }

        private static Expression<Func<Row, object>>? SelectorOf(IModelFilter<Row> filter)
        {
            return (Expression<Func<Row, object>>?)filter.GetType()
                .GetField("_selectExpression", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(filter);
        }

        private sealed class CustomResolver : MemberNameResolver
        {
        }

        private sealed class CountingTypeProvider : IRuntimeTypeProvider
        {
            private readonly RuntimeTypeProvider _inner = new RuntimeTypeProvider(new MemberNameResolver());
            private int _calls;

            public int Calls => Volatile.Read(ref _calls);

            public Type Get(Type sourceType, IEnumerable<System.Reflection.MemberInfo> properties)
            {
                Interlocked.Increment(ref _calls);
                return _inner.Get(sourceType, properties);
            }
        }

        public class LimitRow
        {
            public int X { get; set; }

            public int Y { get; set; }
        }

        public class Row
        {
            public int A { get; set; }

            public string? B { get; set; }

            public double C { get; set; }
        }
    }
}
