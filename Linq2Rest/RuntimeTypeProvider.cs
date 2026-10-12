// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RuntimeTypeProvider.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the RuntimeTypeProvider.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Threading;
    using LinqConvertTools.Parser;

    /// <summary>
    /// Defines the RuntimeTypeProvider.
    /// </summary>
    public class RuntimeTypeProvider : IRuntimeTypeProvider
    {
        /// <summary>
        /// The default for <see cref="MaxRuntimeTypes"/>.
        /// </summary>
        public const int DefaultMaxRuntimeTypes = 4096;

        /// <summary>
        /// The default for <see cref="MaxRuntimeTypesPerSourceType"/>.
        /// </summary>
        public const int DefaultMaxRuntimeTypesPerSourceType = 512;

        private const MethodAttributes GetSetAttr = MethodAttributes.Final | MethodAttributes.Public;
        private static readonly AssemblyName AssemblyName = new AssemblyName { Name = "Linq2RestTypes" };
        private static readonly ModuleBuilder ModuleBuilder;
        private static readonly ConcurrentDictionary<string, Type> BuiltTypes = new ConcurrentDictionary<string, Type>();
        private static readonly object BuildLock = new object();
        private static readonly HashSet<string> DefinedTypeNames = new HashSet<string>(StringComparer.Ordinal);
        private static readonly Dictionary<Type, int> DefinedTypesBySourceType = new Dictionary<Type, int>();
        private static int _maxRuntimeTypes = DefaultMaxRuntimeTypes;
        private static int _maxRuntimeTypesPerSourceType = DefaultMaxRuntimeTypesPerSourceType;
        private static readonly ConcurrentDictionary<Type, CustomAttributeBuilder[]> TypeAttributeBuilders = new ConcurrentDictionary<Type, CustomAttributeBuilder[]>();
        private static readonly ConcurrentDictionary<MemberInfo, CustomAttributeBuilder[]> PropertyAttributeBuilders = new ConcurrentDictionary<MemberInfo, CustomAttributeBuilder[]>();
        private readonly IMemberNameResolver _nameResolver;

        static RuntimeTypeProvider()
        {
            ModuleBuilder =
                AssemblyBuilder.DefineDynamicAssembly(AssemblyName, AssemblyBuilderAccess.Run).DefineDynamicModule(AssemblyName.Name);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeTypeProvider"/> class.
        /// </summary>
        /// <param name="nameResolver"></param>
        public RuntimeTypeProvider(IMemberNameResolver nameResolver)
        {


            _nameResolver = nameResolver;
        }

        /// <summary>
        /// Gets or sets the most runtime types the process defines, across all source types. Each distinct
        /// combination of members a <c>$select</c> names needs its own type, and a type cannot be unloaded once
        /// defined, so this bounds the memory that callers choosing selections can make the process hold. Once the
        /// limit is reached, a combination that has not been built yet fails, and combinations already built keep
        /// working. Attempts that failed to build count toward the limit, since what they defined is not unloaded
        /// either. Defaults to <see cref="DefaultMaxRuntimeTypes"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
        public static int MaxRuntimeTypes
        {
            get => Volatile.Read(ref _maxRuntimeTypes);
            set => Volatile.Write(ref _maxRuntimeTypes, RequirePositive(value));
        }

        /// <summary>
        /// Gets or sets the most runtime types the process defines for any one source type, counted the same way as
        /// <see cref="MaxRuntimeTypes"/>. This keeps callers that request many combinations of one type's members
        /// from using up <see cref="MaxRuntimeTypes"/> for every other type. Defaults to
        /// <see cref="DefaultMaxRuntimeTypesPerSourceType"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
        public static int MaxRuntimeTypesPerSourceType
        {
            get => Volatile.Read(ref _maxRuntimeTypesPerSourceType);
            set => Volatile.Write(ref _maxRuntimeTypesPerSourceType, RequirePositive(value));
        }

        /// <summary>
        /// Gets the number of runtime types defined so far, including attempts that failed to build.
        /// </summary>
        internal static int DefinedTypeCount
        {
            get
            {
                lock (BuildLock)
                {
                    return DefinedTypeNames.Count;
                }
            }
        }

        /// <summary>
        /// Gets the number of runtime types defined so far for <paramref name="sourceType"/>, including attempts that
        /// failed to build.
        /// </summary>
        internal static int DefinedTypeCountFor(Type sourceType)
        {
            lock (BuildLock)
            {
                return DefinedTypesBySourceType.TryGetValue(sourceType, out var count) ? count : 0;
            }
        }

        /// <summary>
        /// Gets whether this provider names properties with the built-in <see cref="MemberNameResolver"/>, so the
        /// types it returns depend only on the source type and the members.
        /// </summary>
        internal bool UsesDefaultNameResolver => _nameResolver.GetType() == typeof(MemberNameResolver);

        /// <summary>
        /// Gets the <see cref="Type"/> matching the provided members.
        /// </summary>
        /// <param name="sourceType">The <see cref="Type"/> to generate the runtime type from.</param>
        /// <param name="properties">The <see cref="MemberInfo"/> to use to generate properties.</param>
        /// <returns>A <see cref="Type"/> mathing the provided properties.</returns>
        public Type Get(Type sourceType, IEnumerable<MemberInfo> properties)
        {
            properties = properties.ToArray();
            if (!properties.Any())
            {
                throw new ArgumentOutOfRangeException("properties", "properties must have at least 1 property definition");
            }

            var dictionary = properties.ToDictionary(f => _nameResolver.ResolveName(f), memberInfo => memberInfo);

            var typeKey = GetTypeKey(sourceType, dictionary);
            if (BuiltTypes.TryGetValue(typeKey, out var builtType))
            {
                return builtType;
            }

            // Defining a type name twice in the module throws, and a name stays defined even when building the type
            // fails, so types are built under a lock and every attempt takes a name that was not defined before.
            lock (BuildLock)
            {
                if (BuiltTypes.TryGetValue(typeKey, out builtType))
                {
                    return builtType;
                }

                DefinedTypesBySourceType.TryGetValue(sourceType, out var definedForSource);
                if (definedForSource >= MaxRuntimeTypesPerSourceType)
                {
                    throw ParseFailure.MarkLimitExceeded(new InvalidOperationException(
                        "The selection needs a new runtime type, and the process has already defined the maximum of "
                        + MaxRuntimeTypesPerSourceType.ToString(CultureInfo.InvariantCulture) + " for " + sourceType.Name + "."));
                }

                if (DefinedTypeNames.Count >= MaxRuntimeTypes)
                {
                    throw ParseFailure.MarkLimitExceeded(new InvalidOperationException(
                        "The selection needs a new runtime type, and the process has already defined the maximum of "
                        + MaxRuntimeTypes.ToString(CultureInfo.InvariantCulture) + "."));
                }

                // Attributes are read before anything is defined, so attributes that cannot be copied fail without
                // taking a type name or a slot in the limits.
                var typeAttributes = GetAttributeBuilders(sourceType);
                var propertyAttributes = dictionary.ToDictionary(x => x.Key, x => GetAttributeBuilders(x.Value));

                var baseName = GetTypeName(sourceType, dictionary);
                var className = baseName;
                for (var attempt = 2; !DefinedTypeNames.Add(className); attempt++)
                {
                    className = baseName + "_" + attempt.ToString(CultureInfo.InvariantCulture);
                }

                DefinedTypesBySourceType[sourceType] = definedForSource + 1;

                var typeBuilder = ModuleBuilder.DefineType(
                    className,
                    TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable);

                foreach (var attributeBuilder in typeAttributes)
                {
                    typeBuilder.SetCustomAttribute(attributeBuilder);
                }

                foreach (var field in dictionary)
                {
                    CreateProperty(typeBuilder, field, propertyAttributes[field.Key]);
                }

                builtType = typeBuilder.CreateTypeInfo() ?? throw new InvalidOperationException("Could not create runtime type " + className + ".");
                BuiltTypes[typeKey] = builtType;
                return builtType;
            }
        }

        private static int RequirePositive(int value)
        {
            return value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum number of runtime types must be at least 1.");
        }

        private static void CreateProperty(TypeBuilder typeBuilder, KeyValuePair<string, MemberInfo> field, IEnumerable<CustomAttributeBuilder> attributeBuilders)
        {


            var propertyType = field.Value.GetMemberType();
            var fieldBuilder = typeBuilder.DefineField("_" + field.Key, propertyType, FieldAttributes.Private);

            var propertyBuilder = typeBuilder.DefineProperty(field.Key, PropertyAttributes.None, propertyType, null);



            foreach (var attributeBuilder in attributeBuilders)
            {
                propertyBuilder.SetCustomAttribute(attributeBuilder);
            }

            var getAccessor = typeBuilder.DefineMethod(
                                                       "get_" + field.Key,
                                                       GetSetAttr,
                                                       propertyType,
                                                       Type.EmptyTypes);

            var getIl = getAccessor.GetILGenerator();
            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldfld, fieldBuilder);
            getIl.Emit(OpCodes.Ret);

            var setAccessor = typeBuilder.DefineMethod(
                                                       "set_" + field.Key,
                                                       GetSetAttr,
                                                       null,
                                                       new[] { propertyType });

            var setIl = setAccessor.GetILGenerator();
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, fieldBuilder);
            setIl.Emit(OpCodes.Ret);

            propertyBuilder.SetGetMethod(getAccessor);
            propertyBuilder.SetSetMethod(setAccessor);
        }

        private static CustomAttributeBuilder[] GetAttributeBuilders(Type type)
        {
            return TypeAttributeBuilders.GetOrAdd(type, t => CreateCustomAttributeBuilders(t.GetCustomAttributesData()));
        }

        private static CustomAttributeBuilder[] GetAttributeBuilders(MemberInfo member)
        {
            return PropertyAttributeBuilders.GetOrAdd(member, m => CreateCustomAttributeBuilders(m.GetCustomAttributesData()));
        }

        private static CustomAttributeBuilder[] CreateCustomAttributeBuilders(IEnumerable<CustomAttributeData> customAttributes)
        {
            return customAttributes
                .Select(
                        x =>
                        {
                            var namedProperties = x.NamedArguments.Where(a => a.MemberInfo is PropertyInfo).ToArray();
                            var namedFields = x.NamedArguments.Where(a => a.MemberInfo is FieldInfo).ToArray();
                            return new CustomAttributeBuilder(
                                x.Constructor,
                                x.ConstructorArguments.Select(GetArgumentValue).ToArray(),
                                namedProperties.Select(a => (PropertyInfo)a.MemberInfo).ToArray(),
                                namedProperties.Select(a => GetArgumentValue(a.TypedValue)).ToArray(),
                                namedFields.Select(a => (FieldInfo)a.MemberInfo).ToArray(),
                                namedFields.Select(a => GetArgumentValue(a.TypedValue)).ToArray());
                        })
                .ToArray();
        }

        /// <summary>
        /// Gets the value of an attribute argument in the form <see cref="CustomAttributeBuilder"/> accepts.
        /// </summary>
        /// <remarks>
        /// Reflection reports an array argument as a read-only collection of its elements, which
        /// <see cref="CustomAttributeBuilder"/> rejects, so arrays are rebuilt with their declared element type.
        /// </remarks>
        private static object GetArgumentValue(CustomAttributeTypedArgument argument)
        {
            if (argument.Value is IReadOnlyCollection<CustomAttributeTypedArgument> elements)
            {
                var elementType = argument.ArgumentType.GetElementType() ?? typeof(object);
                var array = Array.CreateInstance(elementType, elements.Count);
                var index = 0;
                foreach (var element in elements)
                {
                    array.SetValue(GetArgumentValue(element), index++);
                }

                return array;
            }

            // A null argument value is valid; CustomAttributeBuilder accepts null elements.
            return argument.Value!;
        }

        /// <summary>
        /// Identifies a runtime type by everything that shapes it: the full source type, including its namespace
        /// and generic arguments, and for each property its name, the member it copies attributes from, and its type.
        /// </summary>
        private static string GetTypeKey(Type sourceType, Dictionary<string, MemberInfo> fields)
        {
            return fields.Aggregate(
                sourceType.AssemblyQualifiedName,
                (current, field) => current
                    + "|" + field.Key
                    + ":" + field.Value.DeclaringType?.AssemblyQualifiedName + "." + field.Value.Name
                    + ":" + field.Value.GetMemberType().AssemblyQualifiedName);
        }

        private static string GetTypeName(Type sourceType, Dictionary<string, MemberInfo> fields)
        {
            return fields.Aggregate("LinqConvertTools<>" + sourceType.Name, (current, field) => current + (field.Key + field.Value.MemberType));
        }
    }
}