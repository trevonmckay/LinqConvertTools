namespace LinqConvertTools
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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

        /// <summary>
        /// Gets whether <paramref name="member"/>, or for an overriding property any declaration it overrides, carries
        /// a serialization attribute.
        /// </summary>
        /// <remarks>
        /// <see cref="DataMemberAttribute"/> is declared with <c>Inherited = false</c>, so attribute lookups with
        /// inheritance do not find it on an override that does not repeat it; the declarations are checked one by one.
        /// </remarks>
        private static bool IsMarkedForSerialization(MemberInfo member)
        {
            return SelfAndOverriddenDeclarations(member).Any(
                m => m.IsDefined(typeof(DataMemberAttribute), false)
                    || m.IsDefined(typeof(XmlElementAttribute), false)
                    || m.IsDefined(typeof(XmlAttributeAttribute), false));
        }

        private static IEnumerable<MemberInfo> SelfAndOverriddenDeclarations(MemberInfo member)
        {
            yield return member;

            if (member is not PropertyInfo property)
            {
                yield break;
            }

            var accessor = property.GetGetMethod(true) ?? property.GetSetMethod(true);
            var slot = accessor?.GetBaseDefinition();
            if (accessor == null || slot == accessor)
            {
                yield break;
            }

            const BindingFlags DeclaredFlags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            for (var type = property.DeclaringType?.BaseType; type != null; type = type.BaseType)
            {
                foreach (var candidate in type.GetProperties(DeclaredFlags))
                {
                    var candidateAccessor = candidate.GetGetMethod(true) ?? candidate.GetSetMethod(true);
                    if (candidate.Name == property.Name && candidateAccessor?.GetBaseDefinition() == slot)
                    {
                        yield return candidate;
                    }
                }
            }
        }
    }
}
