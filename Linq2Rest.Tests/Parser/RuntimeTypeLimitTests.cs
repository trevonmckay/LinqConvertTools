using System.Collections.Concurrent;
using LinqConvertTools.Parser;
using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// The runtime types built for $select are limited per source type, so one type's selections cannot use up the
    /// budget of every other type.
    /// </summary>
    [TestFixture]
    public class RuntimeTypeLimitTests
    {
        private const int Threads = 8;

        [Test]
        public void SelectionsBeyondTheSourceTypeLimitAreTooComplexOnlyForThatType()
        {
            var flooded = CreateFactory<FloodedRow>();
            var built = flooded.Create("A");
            var original = RuntimeTypeProvider.MaxRuntimeTypesPerSourceType;
            try
            {
                RuntimeTypeProvider.MaxRuntimeTypesPerSourceType = RuntimeTypeProvider.DefinedTypeCountFor(typeof(FloodedRow));

                var exception = Assert.Throws<ODataParseException>(() => flooded.Create("A,B"))!;

                Assert.AreEqual(ODataParseErrorKind.TooComplex, exception.Kind);
                StringAssert.Contains("for FloodedRow", exception.Message);
                Assert.AreSame(built, flooded.Create("A"), "selections built before the limit keep working");
                Assert.IsNotNull(CreateFactory<OtherRow>().Create("A,B"), "other source types keep their own budget");
            }
            finally
            {
                RuntimeTypeProvider.MaxRuntimeTypesPerSourceType = original;
            }

            Assert.IsNotNull(flooded.Create("A,B"), "a selection rejected at the limit is not cached as a failure");
        }

        [Test]
        public void ConcurrentCallersRejectedAtTheLimitEachGetTheirOwnException()
        {
            var resolver = new UnsharedResolver();
            var factory = new SelectExpressionFactory<ContendedRow>(resolver, new SlowLimitProvider());

            for (int round = 0; round < 5; round++)
            {
                using var barrier = new Barrier(Threads);
                var failures = new ConcurrentBag<(string Text, ODataParseException Exception)>();
                var workers = Enumerable.Range(0, Threads)
                    .Select(i => new Thread(() =>
                    {
                        var text = i % 2 == 0 ? "A,B" : "B, A";
                        barrier.SignalAndWait();
                        try
                        {
                            factory.Create(text);
                        }
                        catch (ODataParseException ex)
                        {
                            failures.Add((text, ex));
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

                Assert.AreEqual(Threads, failures.Count);
                foreach (var (text, exception) in failures)
                {
                    Assert.AreEqual(text, exception.Text);
                    Assert.AreEqual(ODataParseErrorKind.TooComplex, exception.Kind);
                }

                Assert.AreEqual(Threads, failures.Select(x => x.Exception.InnerException).Distinct().Count(), "no inner exception is shared between callers");
            }
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void SourceTypeLimitMustBePositive(int limit)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RuntimeTypeProvider.MaxRuntimeTypesPerSourceType = limit);
        }

        private static SelectExpressionFactory<T> CreateFactory<T>()
        {
            var resolver = new MemberNameResolver();
            return new SelectExpressionFactory<T>(resolver, new RuntimeTypeProvider(resolver));
        }

        private sealed class UnsharedResolver : MemberNameResolver
        {
        }

        /// <summary>
        /// Takes long enough to fail that concurrent callers of one selection wait on the same build.
        /// </summary>
        private sealed class SlowLimitProvider : IRuntimeTypeProvider
        {
            public Type Get(Type sourceType, IEnumerable<System.Reflection.MemberInfo> properties)
            {
                Thread.Sleep(50);
                throw ParseFailure.MarkLimitExceeded(new InvalidOperationException("limit"));
            }
        }

        public class FloodedRow
        {
            public int A { get; set; }

            public int B { get; set; }
        }

        public class OtherRow
        {
            public int A { get; set; }

            public int B { get; set; }
        }

        public class ContendedRow
        {
            public int A { get; set; }

            public int B { get; set; }
        }
    }
}
