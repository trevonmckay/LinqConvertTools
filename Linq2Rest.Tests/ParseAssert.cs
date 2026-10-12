using NUnit.Framework;

namespace LinqConvertTools.Tests
{
    /// <summary>
    /// Asserts that parsing failed with an <see cref="ODataParseException"/> wrapping the original failure.
    /// </summary>
    internal static class ParseAssert
    {
        public static ODataParseException Throws<TInner>(TestDelegate code, ODataParseErrorKind kind = ODataParseErrorKind.Invalid)
            where TInner : Exception
        {
            return Throws(typeof(TInner), code, kind);
        }

        public static ODataParseException Throws(Type innerType, TestDelegate code, ODataParseErrorKind kind = ODataParseErrorKind.Invalid)
        {
            var exception = Assert.Throws<ODataParseException>(code)!;
            AssertWraps(exception, innerType, kind);
            return exception;
        }

        public static ODataParseException ThrowsTooDeep(TestDelegate code)
        {
            return Throws<InvalidOperationException>(code, ODataParseErrorKind.TooComplex);
        }

        public static void IsWrapped<TInner>(Exception? thrown, ODataParseErrorKind kind)
            where TInner : Exception
        {
            Assert.IsInstanceOf<ODataParseException>(thrown);
            AssertWraps((ODataParseException)thrown!, typeof(TInner), kind);
        }

        private static void AssertWraps(ODataParseException exception, Type innerType, ODataParseErrorKind kind)
        {
            Assert.IsNotNull(exception.InnerException);
            Assert.AreEqual(innerType, exception.InnerException!.GetType());
            Assert.AreEqual(kind, exception.Kind);
        }
    }
}
