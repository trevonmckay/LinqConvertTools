namespace LinqConvertTools.Parser
{
    using System;
    using System.IO;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Reports a failure caused by the text of a query option as an <see cref="ODataParseException"/>.
    /// </summary>
    internal static class ParseFailure
    {
        private const string LimitExceededKey = "LinqConvertTools.ParseLimitExceeded";

        /// <summary>
        /// Marks <paramref name="exception"/> as a failure to stay within a parser limit, so it is reported as
        /// <see cref="ODataParseErrorKind.TooComplex"/>. The mark is kept in <see cref="Exception.Data"/>, so the
        /// exception keeps its public type and survives serialization.
        /// </summary>
        public static TException MarkLimitExceeded<TException>(TException exception)
            where TException : Exception
        {
            exception.Data[LimitExceededKey] = true;
            return exception;
        }

        /// <summary>
        /// Runs <paramref name="parse"/>, wrapping any failure caused by <paramref name="text"/> in an
        /// <see cref="ODataParseException"/>.
        /// </summary>
        /// <remarks>
        /// The parser builds expressions with <see cref="System.Linq.Expressions.Expression"/> factory methods,
        /// which reject input the parser did not validate first with <see cref="ArgumentException"/> or
        /// <see cref="InvalidOperationException"/>. Those types cannot be told apart from the parser's own, so
        /// every failure is wrapped except the types that only a defect or a broken runtime environment produces,
        /// which must surface as server errors rather than as bad input.
        /// </remarks>
        public static TResult Guard<TResult>(string queryOption, string? text, Func<TResult> parse)
        {
            try
            {
                return parse();
            }
            catch (Exception exception) when (IsCausedByText(exception))
            {
                throw Wrap(queryOption, text, exception);
            }
        }

        /// <summary>
        /// Runs <paramref name="run"/>, wrapping only a failure to stay within a parser limit in an
        /// <see cref="ODataParseException"/> and letting every other failure through.
        /// </summary>
        /// <remarks>
        /// Used around work that runs after the text has been validated, such as building a runtime type for a
        /// selection, where any failure other than a limit is a server-side fault.
        /// </remarks>
        public static TResult GuardLimits<TResult>(string queryOption, string? text, Func<TResult> run)
        {
            try
            {
                return run();
            }
            catch (Exception exception) when (IsLimit(exception))
            {
                throw Wrap(queryOption, text, exception);
            }
        }

        private static ODataParseException Wrap(string queryOption, string? text, Exception exception)
        {
            var kind = IsLimit(exception) ? ODataParseErrorKind.TooComplex : ODataParseErrorKind.Invalid;
            return new ODataParseException(
                "The " + queryOption + " query option could not be parsed. " + exception.Message,
                queryOption,
                text,
                kind,
                exception);
        }

        private static bool IsCausedByText(Exception exception)
        {
            return exception is not (ODataParseException
                or NullReferenceException
                or IndexOutOfRangeException
                or InvalidCastException
                or ArgumentNullException
                or ArgumentOutOfRangeException
                or ObjectDisposedException
                or NotImplementedException
                or OutOfMemoryException
                or AccessViolationException
                or TypeInitializationException
                or TypeLoadException
                or MissingMemberException
                or PlatformNotSupportedException
                or BadImageFormatException
                or FileLoadException
                or FileNotFoundException);
        }

        private static bool IsLimit(Exception exception)
        {
            return exception.Data[LimitExceededKey] is true
                || exception is InsufficientExecutionStackException
                or RegexMatchTimeoutException;
        }
    }
}
