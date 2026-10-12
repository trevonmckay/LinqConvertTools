namespace LinqConvertTools
{
    /// <summary>
    /// Describes why the text of an OData query option could not be parsed.
    /// </summary>
    public enum ODataParseErrorKind
    {
        /// <summary>
        /// The text is not a query the parser can read against the requested type, such as invalid syntax, an
        /// unknown member or path segment, a function called with the wrong number or types of arguments, or a
        /// literal that cannot be read as the type it is compared with.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// The text exceeds a limit the parser enforces: it nests deeper than the maximum depth, needs more stack
        /// than the calling thread has, or takes longer to tokenize than a regular expression match allows.
        /// The same text can be accepted with a higher limit, or on a thread with a larger stack.
        /// </summary>
        TooComplex = 1,
    }
}
