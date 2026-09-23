using NUnit.Framework;
using System.Linq.Expressions;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// The OData date functions <c>year()</c>, <c>month()</c>, <c>day()</c>, <c>hour()</c>, <c>minute()</c> and <c>second()</c>
    /// apply to <see cref="DateTime"/> and <see cref="DateTimeOffset"/> members, nullable or not.
    /// </summary>
    [TestFixture]
    public class DateFunctionTests
    {
        private ODataExpressionConverter _converter = null!;
        private Event[] _events = null!;

        [SetUp]
        public void Setup()
        {
            _converter = new ODataExpressionConverter();
            _events = new[]
            {
                new Event
                {
                    Name = "Launch",
                    At = new DateTime(2024, 3, 15, 9, 30, 45, DateTimeKind.Utc),
                    Offset = new DateTimeOffset(2024, 3, 15, 9, 30, 45, TimeSpan.Zero),
                    MaybeAt = new DateTime(2024, 3, 15, 9, 30, 45, DateTimeKind.Utc),
                    MaybeOffset = new DateTimeOffset(2024, 3, 15, 9, 30, 45, TimeSpan.Zero),
                },
                new Event
                {
                    Name = "Review",
                    At = new DateTime(2025, 11, 2, 23, 5, 10, DateTimeKind.Utc),
                    Offset = new DateTimeOffset(2025, 11, 3, 1, 5, 10, TimeSpan.FromHours(2)),
                },
            };
        }

        [TestCase("year(Offset) eq 2024", "Launch")]
        [TestCase("month(Offset) eq 11", "Review")]
        [TestCase("day(Offset) eq 15", "Launch")]
        [TestCase("hour(Offset) eq 9", "Launch")]
        [TestCase("minute(Offset) eq 5", "Review")]
        [TestCase("second(Offset) eq 45", "Launch")]
        [TestCase("year(Offset) ge 2025", "Review")]
        [TestCase("year(Offset) eq 2024 and month(Offset) eq 3", "Launch")]
        [TestCase("year(At) eq 2025", "Review")]
        [TestCase("year(MaybeOffset) eq 2024", "Launch")]
        [TestCase("year(MaybeOffset) ne 2024", "Review")]
        [TestCase("month(MaybeOffset) lt 12", "Launch")]
        [TestCase("year(MaybeAt) eq 2024", "Launch")]
        [TestCase("year(MaybeAt) ne 2024", "Review")]
        public void FiltersByDatePart(string filter, string expected)
        {
            var predicate = _converter.Convert<Event>(filter);

            string actual = string.Join(",", _events.AsQueryable().Where(predicate).Select(e => e.Name));

            Assert.AreEqual(expected, actual, "Failed for " + predicate);
        }

        [Test]
        public void DateTimeOffsetPartsUseTheValuesOffset()
        {
            // 2025-11-03T01:05:10+02:00 is 2025-11-02T23:05:10Z; DateTimeOffset reads its parts in its own offset.
            Assert.AreEqual("Review", Filter("day(Offset) eq 3 and hour(Offset) eq 1"));
        }

        [TestCase("year(Offset) eq 2024", "x => (x.Offset.Year == 2024)")]
        [TestCase("year(MaybeOffset) eq 2024", "x => (IIF(x.MaybeOffset.HasValue, Convert(x.MaybeOffset.Value.Year, Nullable`1), null) == Convert(2024, Nullable`1))")]
        public void CreatesDatePartExpression(string filter, string expected)
        {
            Assert.AreEqual(expected, _converter.Convert<Event>(filter).ToString());
        }

        [TestCase("year(Name) eq 2024")]
        [TestCase("hour(Duration) eq 1")]
        public void RejectsNonDateMembers(string filter)
        {
            Assert.Throws<InvalidOperationException>(() => _converter.Convert<Event>(filter));
        }

        [Test]
        public void WritesDateTimeOffsetParts()
        {
            Expression<Func<Event, bool>> expression = x => x.Offset.Year == 2024 && x.Offset.Hour == 9;

            Assert.AreEqual("year(Offset) eq 2024 and hour(Offset) eq 9", _converter.Convert(expression));
        }

        private string Filter(string filter)
        {
            return string.Join(",", _events.AsQueryable().Where(_converter.Convert<Event>(filter)).Select(e => e.Name));
        }

        public class Event
        {
            public string Name { get; set; } = string.Empty;

            public DateTime At { get; set; }

            public DateTimeOffset Offset { get; set; }

            public DateTime? MaybeAt { get; set; }

            public DateTimeOffset? MaybeOffset { get; set; }

            public TimeSpan Duration { get; set; }
        }
    }
}
