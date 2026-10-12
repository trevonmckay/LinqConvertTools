namespace LinqConvertTools
{
    using System.Reflection;

    /// <summary>
    /// Decides which members of a type the query options may read.
    /// </summary>
    internal static class QueryableMembers
    {
        /// <summary>
        /// Gets whether <paramref name="member"/> holds a value a query can read: a field, or a property that has a
        /// getter and no index parameters.
        /// </summary>
        public static bool IsReadableValue(MemberInfo member)
        {
            return member switch
            {
                FieldInfo => true,
                PropertyInfo property => property.GetIndexParameters().Length == 0 && property.GetGetMethod(true) != null,
                _ => false,
            };
        }
    }
}
