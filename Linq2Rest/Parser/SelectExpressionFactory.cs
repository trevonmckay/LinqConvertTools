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
    using System.Collections.Concurrent;
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

        /// <summary>
        /// The selections built by every factory for <typeparamref name="T"/> that uses the built-in name resolver and
        /// runtime type provider, whose results depend only on the selection.
        /// </summary>
        private static readonly SelectionCache SharedSelections = new SelectionCache();

        private readonly SelectionCache _knownSelections;
        private readonly IMemberNameResolver _nameResolver;
        private readonly IRuntimeTypeProvider _runtimeTypeProvider;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectExpressionFactory{T}"/> class.
        /// </summary>
        public SelectExpressionFactory(IMemberNameResolver nameResolver, IRuntimeTypeProvider runtimeTypeProvider)
        {
            _nameResolver = nameResolver;
            _runtimeTypeProvider = runtimeTypeProvider;
            _knownSelections = UsesBuiltInServices(nameResolver, runtimeTypeProvider)
                ? SharedSelections
                : new SelectionCache();
        }

        /// <summary>
        /// Creates a select expression.
        /// </summary>
        /// <param name="selection">The properties to select.</param>
        /// <returns>An instance of a <see cref="Func{T1,TResult}"/>, or <see langword="null"/> when <paramref name="selection"/> names no properties.</returns>
        /// <exception cref="ODataParseException">The selection names a member that <typeparamref name="T"/> does not have, names a member more than once, or needs a runtime type beyond <see cref="RuntimeTypeProvider.MaxRuntimeTypes"/> or <see cref="RuntimeTypeProvider.MaxRuntimeTypesPerSourceType"/>.</exception>
        public Expression<Func<T, object>>? Create(string? selection)
        {
            var fieldNames = (selection ?? string.Empty).Split(',')
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .OrderBy(x => x, StringComparer.InvariantCulture)
                .ToArray();

            if (fieldNames.Length == 0)
            {
                return null;
            }

            var key = string.Join(",", fieldNames);
            if (_knownSelections.TryGet(key, out var knownSelection))
            {
                return knownSelection;
            }

            var sourceMembers = ParseFailure.Guard(StringConstants.SelectParameter, selection, () => ResolveMembers(fieldNames));

            return ParseFailure.GuardLimits(
                StringConstants.SelectParameter,
                selection,
                () => _knownSelections.GetOrBuild(key, () => CreateSelector(sourceMembers)));
        }

        private static bool UsesBuiltInServices(IMemberNameResolver nameResolver, IRuntimeTypeProvider runtimeTypeProvider)
        {
            return nameResolver.GetType() == typeof(MemberNameResolver)
                && runtimeTypeProvider.GetType() == typeof(RuntimeTypeProvider)
                && ((RuntimeTypeProvider)runtimeTypeProvider).UsesDefaultNameResolver;
        }

        private Dictionary<string, MemberInfo> ResolveMembers(IEnumerable<string> fieldNames)
        {
            var elementType = typeof(T);
            var elementMembers = elementType.GetProperties(Flags)
                .Cast<MemberInfo>()
                .Concat(elementType.GetFields(Flags))
                .Where(QueryableMembers.IsQueryable)
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

        /// <summary>
        /// Holds the selectors built for each selection, building each one once however many threads ask for it at
        /// the same time. It stops adding selections once it holds about
        /// <see cref="RuntimeTypeProvider.MaxRuntimeTypesPerSourceType"/>, the most runtime types one source type can
        /// have; threads adding at the same moment can take it a few past that.
        /// </summary>
        private sealed class SelectionCache
        {
            private readonly ConcurrentDictionary<string, Entry> _entries =
                new ConcurrentDictionary<string, Entry>(StringComparer.Ordinal);

            private int _count;

            public bool TryGet(string key, out Expression<Func<T, object>>? selector)
            {
                if (_entries.TryGetValue(key, out var entry) && entry.Selector.IsValueCreated)
                {
                    selector = entry.Selector.Value;
                    return true;
                }

                selector = null;
                return false;
            }

            /// <summary>
            /// Returns the selector cached for <paramref name="key"/>, building it with <paramref name="build"/> when
            /// there is none. A build that fails is not cached, so the next request builds again.
            /// </summary>
            /// <remarks>
            /// A failed <see cref="Lazy{T}"/> rethrows one stored exception to every thread waiting on it. Only the
            /// thread that ran the failed build rethrows it; a waiting thread removes the failed entry and builds
            /// again, so every caller gets an exception of its own.
            /// </remarks>
            public Expression<Func<T, object>> GetOrBuild(string key, Func<Expression<Func<T, object>>> build)
            {
                while (true)
                {
                    if (!_entries.TryGetValue(key, out var entry))
                    {
                        if (Volatile.Read(ref _count) >= RuntimeTypeProvider.MaxRuntimeTypesPerSourceType)
                        {
                            return build();
                        }

                        var created = new Entry(build);
                        entry = _entries.GetOrAdd(key, created);
                        if (ReferenceEquals(entry, created))
                        {
                            Interlocked.Increment(ref _count);
                        }
                    }

                    try
                    {
                        return entry.Selector.Value;
                    }
                    catch
                    {
                        Remove(key, entry);
                        if (entry.WasBuiltOnCurrentThread)
                        {
                            throw;
                        }
                    }
                }
            }

            private void Remove(string key, Entry entry)
            {
                if (((ICollection<KeyValuePair<string, Entry>>)_entries).Remove(new KeyValuePair<string, Entry>(key, entry)))
                {
                    Interlocked.Decrement(ref _count);
                }
            }

            private sealed class Entry
            {
                private int _builderThreadId;

                public Entry(Func<Expression<Func<T, object>>> build)
                {
                    Selector = new Lazy<Expression<Func<T, object>>>(
                        () =>
                        {
                            Volatile.Write(ref _builderThreadId, Environment.CurrentManagedThreadId);
                            return build();
                        },
                        LazyThreadSafetyMode.ExecutionAndPublication);
                }

                public Lazy<Expression<Func<T, object>>> Selector { get; }

                public bool WasBuiltOnCurrentThread => Volatile.Read(ref _builderThreadId) == Environment.CurrentManagedThreadId;
            }
        }
    }
}