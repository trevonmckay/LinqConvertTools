// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SelectExpressionFactory.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the SelectExpressionFactory.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Threading;

    /// <summary>
    /// Defines the SelectExpressionFactory.
    /// </summary>
    /// <typeparam name="T">The <see cref="Type"/> of object to project.</typeparam>
    public class SelectExpressionFactory<T> : ISelectExpressionFactory<T>
    {
        private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private readonly IDictionary<string, Expression<Func<T, object>>?> _knownSelections;
        private readonly IMemberNameResolver _nameResolver;
        private readonly IRuntimeTypeProvider _runtimeTypeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectExpressionFactory{T}"/> class.
        /// </summary>
        public SelectExpressionFactory(IMemberNameResolver nameResolver, IRuntimeTypeProvider runtimeTypeProvider)
        {



            _nameResolver = nameResolver;
            _runtimeTypeProvider = runtimeTypeProvider;
            _knownSelections = new Dictionary<string, Expression<Func<T, object>>?>
                                   {
                                       { string.Empty, null }
                                   };
        }

        /// <summary>
        /// Creates a select expression.
        /// </summary>
        /// <param name="selection">The properties to select.</param>
        /// <returns>An instance of a <see cref="Func{T1,TResult}"/>, or <see langword="null"/> when <paramref name="selection"/> names no properties.</returns>
        /// <exception cref="ODataParseException">The selection names a member that <typeparamref name="T"/> does not have, or names a member more than once.</exception>
        public Expression<Func<T, object>>? Create(string? selection)
        {
            var fieldNames = (selection ?? string.Empty).Split(',')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .OrderBy(x => x)
                .ToArray();

            var key = string.Join(",", fieldNames);

            if (_knownSelections.ContainsKey(key))
            {
                var knownSelection = _knownSelections[key];

                return knownSelection;
            }

            var sourceMembers = ParseFailure.Guard(StringConstants.SelectParameter, selection, () => ResolveMembers(fieldNames));
            var selector = CreateSelector(sourceMembers);

            if (Monitor.TryEnter(_knownSelections, 1000))
            {
                _knownSelections.Add(key, selector);

                Monitor.Exit(_knownSelections);
            }

            return selector;
        }

        private Dictionary<string, MemberInfo> ResolveMembers(IEnumerable<string> fieldNames)
        {
            var elementType = typeof(T);
            var elementMembers = elementType.GetProperties(Flags)
                .Cast<MemberInfo>()
                .Concat(elementType.GetFields(Flags))
                .ToArray();
            var sourceMembers = new Dictionary<string, MemberInfo>();
            foreach (var name in fieldNames)
            {
                if (sourceMembers.ContainsKey(name))
                {
                    throw new FormatException(name + " is selected more than once");
                }

                sourceMembers.Add(
                    name,
                    elementMembers.FirstOrDefault(m => _nameResolver.ResolveName(m) == name)
                        ?? throw new FormatException(name + " is not recognized as a valid property"));
            }

            return sourceMembers;
        }

        private Expression<Func<T, object>> CreateSelector(IDictionary<string, MemberInfo> sourceMembers)
        {
            var elementType = typeof(T);
            var dynamicType = _runtimeTypeProvider.Get(elementType, sourceMembers.Values);

            var sourceItem = Expression.Parameter(elementType, "t");
            var bindings = dynamicType
                .GetProperties()
                .Select(p =>
                            {
                                var member = sourceMembers[p.Name];
                                var expression = member.MemberType == MemberTypes.Property
                                                    ? Expression.Property(sourceItem, (PropertyInfo)member)
                                                    : Expression.Field(sourceItem, (FieldInfo)member);
                                return Expression.Bind(p, expression);
                            });

            var constructorInfo = dynamicType.GetConstructor(Type.EmptyTypes);



            return Expression.Lambda<Func<T, object>>(
                                                      Expression.MemberInit(Expression.New(constructorInfo), bindings),
                                                      sourceItem);
        }

        [ContractInvariantMethod]
        private void Invariants()
        {



        }
    }
}