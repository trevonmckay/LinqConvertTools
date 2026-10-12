namespace LinqConvertTools
{
    using System;
    using System.Runtime.Serialization;

    /// <summary>
    /// The text of an OData query option, such as <c>$filter</c> or <c>$orderby</c>, could not be parsed.
    /// </summary>
    /// <remarks>
    /// Every failure caused by the query text is reported with this exception, and
    /// <see cref="Exception.InnerException"/> holds the original failure. Failures that indicate a defect rather
    /// than bad input, such as <see cref="NullReferenceException"/> or <see cref="ArgumentNullException"/>, are
    /// not wrapped.
    /// </remarks>
    [Serializable]
    public class ODataParseException : Exception
    {
        private const string QueryOptionKey = "QueryOption";
        private const string TextKey = "Text";
        private const string KindKey = "Kind";

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataParseException"/> class.
        /// </summary>
        public ODataParseException()
            : this("The query could not be parsed.")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataParseException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the failure.</param>
        public ODataParseException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataParseException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the failure.</param>
        /// <param name="innerException">The failure that caused this exception.</param>
        public ODataParseException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataParseException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the failure.</param>
        /// <param name="queryOption">The query option whose text could not be parsed, such as <c>$filter</c>.</param>
        /// <param name="text">The text that could not be parsed.</param>
        /// <param name="kind">Why the text could not be parsed.</param>
        /// <param name="innerException">The failure that caused this exception.</param>
        public ODataParseException(string message, string? queryOption, string? text, ODataParseErrorKind kind, Exception? innerException)
            : base(message, innerException)
        {
            QueryOption = queryOption;
            Text = text;
            Kind = kind;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataParseException"/> class with serialized data.
        /// </summary>
        /// <param name="info">The serialized object data.</param>
        /// <param name="context">The source of the serialized data.</param>
        protected ODataParseException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            QueryOption = info.GetString(QueryOptionKey);
            Text = info.GetString(TextKey);
            Kind = (ODataParseErrorKind)info.GetInt32(KindKey);
        }

        /// <summary>
        /// Gets the query option whose text could not be parsed: <c>$filter</c>, <c>$orderby</c>, <c>$select</c>,
        /// <c>$skip</c> or <c>$top</c>.
        /// </summary>
        public string? QueryOption { get; }

        /// <summary>
        /// Gets the text of the query option that could not be parsed.
        /// </summary>
        /// <remarks>
        /// The text comes from the caller's request and can contain personal data, so take the same care logging
        /// it as logging the request itself.
        /// </remarks>
        public string? Text { get; }

        /// <summary>
        /// Gets why the text could not be parsed.
        /// </summary>
        public ODataParseErrorKind Kind { get; }

        /// <inheritdoc />
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(QueryOptionKey, QueryOption);
            info.AddValue(TextKey, Text);
            info.AddValue(KindKey, (int)Kind);
        }
    }
}
