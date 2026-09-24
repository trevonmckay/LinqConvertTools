namespace LinqConvertTools.Parser
{
    using System;

    /// <summary>
    /// Settings shared by the regular expressions that tokenize a filter.
    /// </summary>
    internal static class ParserRegex
    {
        /// <summary>
        /// The longest a single match may run. Filters come from untrusted callers, so a pattern that
        /// backtracks badly on hostile input throws <see cref="System.Text.RegularExpressions.RegexMatchTimeoutException"/>
        /// instead of holding a thread.
        /// </summary>
        public static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(1);
    }
}
