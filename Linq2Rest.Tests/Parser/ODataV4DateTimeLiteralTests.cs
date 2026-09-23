using LinqConvertTools;
using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// OData v4 writes DateTimeOffset (and date/time) values as bare ISO-8601 literals — <c>At ge 2026-10-01T00:00:00Z</c> —
    /// rather than the v3 typed form <c>datetimeoffset'2026-10-01T00:00:00Z'</c>. Both must convert, for nullable and
    /// non-nullable members, through the public <see cref="ODataExpressionConverter"/>.
    /// </summary>
    [TestFixture]
    public class ODataV4DateTimeLiteralTests
    {
        private static readonly DateTimeOffset Expected = new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);

        private ODataExpressionConverter _converter;

        [SetUp]
        public void Setup()
        {
            _converter = new ODataExpressionConverter();
        }

        [TestCase("At ge 2026-10-01T00:00:00Z")]
        [TestCase("At ge 2026-10-01T00:00Z")]
        [TestCase("At ge 2026-10-01T00:00:00.0000000Z")]
        [TestCase("At ge 2026-10-01T02:00:00+02:00")]
        [TestCase("At ge 2026-09-30T19:00:00-05:00")]
        [TestCase("At ge datetimeoffset'2026-10-01T00:00:00Z'")]
        public void WhenFilteringDateTimeOffsetThenBareAndTypedLiteralsConvert(string filter)
        {
            var predicate = _converter.Convert<IHasDateTimeOffset>(filter).Compile();

            Assert.IsTrue(predicate(new HasDateTimeOffset(Expected)), "Failed for " + filter);
            Assert.IsFalse(predicate(new HasDateTimeOffset(Expected.AddTicks(-1))), "Failed for " + filter);
        }

        [TestCase("At ge 2026-10-01T00:00:00Z and At lt 2026-10-03T00:00:00Z")]
        [TestCase("At ge datetimeoffset'2026-10-01T00:00:00Z' and At lt datetimeoffset'2026-10-03T00:00:00Z'")]
        public void WhenFilteringNullableDateTimeOffsetThenBareAndTypedLiteralsConvert(string filter)
        {
            var predicate = _converter.Convert<IHasNullableDateTimeOffset>(filter).Compile();

            Assert.IsTrue(predicate(new HasNullableDateTimeOffset(Expected.AddDays(1))), "Failed for " + filter);
            Assert.IsFalse(predicate(new HasNullableDateTimeOffset(Expected.AddDays(3))), "Failed for " + filter);
            Assert.IsFalse(predicate(new HasNullableDateTimeOffset(null)), "Failed for " + filter);
        }

        [TestCase("At ge 2026-10-01")]
        [TestCase("At ge 2026-10-01T00:00:00")]
        [TestCase("At ge 2026-10-01T00:00:00 02:00")]
        [TestCase("At ge 20261001T000000Z")]
        public void WhenDateTimeOffsetLiteralIsNotV4ThenThrows(string filter)
        {
            Assert.Throws<FormatException>(() => _converter.Convert<IHasDateTimeOffset>(filter));
        }

        [TestCase("Moment eq 2026-10-01T00:00:00Z", DateTimeKind.Utc)]
        [TestCase("Moment eq 2026-10-01T00:00:00", DateTimeKind.Unspecified)]
        [TestCase("Moment eq 2026-10-01", DateTimeKind.Unspecified)]
        public void WhenFilteringDateTimeThenBareLiteralConverts(string filter, DateTimeKind kind)
        {
            var predicate = _converter.Convert<IHasDateTime>(filter).Compile();

            Assert.IsTrue(predicate(new HasDateTime(DateTime.SpecifyKind(new DateTime(2026, 10, 1), kind))), "Failed for " + filter);
            Assert.IsFalse(predicate(new HasDateTime(DateTime.SpecifyKind(new DateTime(2026, 10, 2), kind))), "Failed for " + filter);
        }

        public interface IHasDateTimeOffset
        {
            DateTimeOffset At { get; }
        }

        public interface IHasNullableDateTimeOffset
        {
            DateTimeOffset? At { get; }
        }

        public interface IHasDateTime
        {
            DateTime Moment { get; }
        }

        private sealed record HasDateTimeOffset(DateTimeOffset At) : IHasDateTimeOffset;

        private sealed record HasNullableDateTimeOffset(DateTimeOffset? At) : IHasNullableDateTimeOffset;

        private sealed record HasDateTime(DateTime Moment) : IHasDateTime;
    }
}
