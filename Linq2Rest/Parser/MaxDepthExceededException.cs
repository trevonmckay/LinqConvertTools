namespace LinqConvertTools.Parser
{
    using System;

    /// <summary>
    /// A filter nests deeper than <see cref="FilterExpressionFactory.MaxDepth"/>. It is reported to callers as the
    /// <see cref="Exception.InnerException"/> of an <see cref="ODataParseException"/> of kind
    /// <see cref="ODataParseErrorKind.TooComplex"/>.
    /// </summary>
    internal sealed class MaxDepthExceededException : InvalidOperationException
    {
        public MaxDepthExceededException()
        {
        }

        public MaxDepthExceededException(string message)
            : base(message)
        {
        }

        public MaxDepthExceededException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
