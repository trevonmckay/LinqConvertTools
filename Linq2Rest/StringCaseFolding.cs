namespace LinqConvertTools
{
    /// <summary>
    /// Selects the string methods used when a filter converts string case, both for case-insensitive
    /// comparisons (<c>ignoreCase</c>) and for the OData <c>toupper()</c> and <c>tolower()</c> functions.
    /// </summary>
    public enum StringCaseFolding
    {
        /// <summary>
        /// Uses <see cref="string.ToUpper()"/> and <see cref="string.ToLower()"/>.
        /// EF Core providers, including SQL Server and PostgreSQL, translate these to SQL <c>UPPER</c> and <c>LOWER</c>,
        /// so the database's rules decide how case is converted.
        /// When the expression runs in memory, case is converted using the current culture.
        /// </summary>
        CurrentCulture = 0,

        /// <summary>
        /// Uses <see cref="string.ToUpperInvariant()"/> and <see cref="string.ToLowerInvariant()"/>.
        /// Case conversion in memory is culture-independent, but EF Core providers cannot translate these methods,
        /// so queries against a database throw.
        /// </summary>
        Invariant = 1,
    }
}
