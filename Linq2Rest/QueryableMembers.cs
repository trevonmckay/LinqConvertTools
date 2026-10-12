namespace LinqConvertTools
{
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Xml.Serialization;

    /// <summary>
    /// Decides which members of a type the query options may read.
    /// </summary>
    internal static class QueryableMembers
    {
        /// <summary>
        /// Gets whether a query option may read <paramref name="member"/>: it holds a readable value, and it is
        /// public or is marked for serialization.
        /// </summary>
        /// <remarks>
        /// Query text comes from the caller's request, so a non-public member must be opted in explicitly; otherwise
        /// a request could filter or sort on, or project, private state such as a stored hash or an internal cost.
        /// </remarks>
        public static bool IsQueryable(MemberInfo member)
        {
            return IsReadableValue(member) && (IsPublic(member) || IsMarkedForSerialization(member));
        }

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

        private static bool IsPublic(MemberInfo member)
        {
            return member switch
            {
                FieldInfo field => field.IsPublic,
                PropertyInfo property => property.GetGetMethod(false) != null,
                _ => false,
            };
        }

        private static bool IsMarkedForSerialization(MemberInfo member)
        {
            return member.IsDefined(typeof(DataMemberAttribute), true)
                || member.IsDefined(typeof(XmlElementAttribute), true)
                || member.IsDefined(typeof(XmlAttributeAttribute), true);
        }
    }
}
