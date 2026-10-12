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

        private const MethodAttributes GetSetAttr = MethodAttributes.Final | MethodAttributes.Public;
        private static readonly AssemblyName AssemblyName = new AssemblyName { Name = "Linq2RestTypes" };
        private static readonly ModuleBuilder ModuleBuilder;
        private static readonly ConcurrentDictionary<string, Type> BuiltTypes = new ConcurrentDictionary<string, Type>();
        private static readonly object BuildLock = new object();
        private static readonly HashSet<string> DefinedTypeNames = new HashSet<string>(StringComparer.Ordinal);
        private static int _maxRuntimeTypes = DefaultMaxRuntimeTypes;
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
        /// Gets or sets the most runtime types the process builds, across all source types. Each distinct
        /// combination of members a <c>$select</c> names needs its own type, and a type cannot be unloaded once
        /// built, so this bounds the memory that callers choosing selections can make the process hold. Once the
        /// limit is reached, a combination that has not been built yet fails, and combinations already built keep
        /// working. Defaults to <see cref="DefaultMaxRuntimeTypes"/>.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
        public static int MaxRuntimeTypes
        {
            get => Volatile.Read(ref _maxRuntimeTypes);
            set => Volatile.Write(
                ref _maxRuntimeTypes,
                value >= 1 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum number of runtime types must be at least 1."));
        }

        /// <summary>
        /// Gets the number of runtime types built so far.
        /// </summary>
        internal static int BuiltTypeCount => BuiltTypes.Count;

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

                if (BuiltTypes.Count >= MaxRuntimeTypes)
                {
                    throw ParseFailure.MarkLimitExceeded(new InvalidOperationException(
                        "The selection needs a new runtime type, and the process has already built the maximum of "
                        + MaxRuntimeTypes.ToString(CultureInfo.InvariantCulture) + "."));
                }

                var baseName = GetTypeName(sourceType, dictionary);
                var className = baseName;
                for (var attempt = 2; !DefinedTypeNames.Add(className); attempt++)
                {
                    className = baseName + "_" + attempt.ToString(CultureInfo.InvariantCulture);
                }

                var typeBuilder = ModuleBuilder.DefineType(
                    className,
                    TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Serializable);

                SetAttributes(typeBuilder, sourceType);

                foreach (var field in dictionary)
                {
                    CreateProperty(typeBuilder, field);
                }

                builtType = typeBuilder.CreateTypeInfo() ?? throw new InvalidOperationException("Could not create runtime type " + className + ".");
                BuiltTypes[typeKey] = builtType;
                return builtType;
            }
        }

        private static void CreateProperty(TypeBuilder typeBuilder, KeyValuePair<string, MemberInfo> field)
        {


            var propertyType = GetValueType(field.Value);
            var fieldBuilder = typeBuilder.DefineField("_" + field.Key, propertyType, FieldAttributes.Private);

            var propertyBuilder = typeBuilder.DefineProperty(field.Key, PropertyAttributes.None, propertyType, null);



            SetAttributes(propertyBuilder, field.Value);

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

        private static void SetAttributes(TypeBuilder typeBuilder, Type type)
        {



            var attributeBuilders = TypeAttributeBuilders
                .GetOrAdd(
                          type,
                          t =>
                          {
                              var customAttributes = t.GetCustomAttributesData();
                              return CreateCustomAttributeBuilders(customAttributes).ToArray();
                          });



            foreach (var attributeBuilder in attributeBuilders)
            {
                typeBuilder.SetCustomAttribute(attributeBuilder);
            }
        }

        private static void SetAttributes(PropertyBuilder propertyBuilder, MemberInfo memberInfo)
        {



            var customAttributeBuilders = PropertyAttributeBuilders
                .GetOrAdd(
                          memberInfo,
                          p =>
                          {
                              var customAttributes = p.GetCustomAttributesData();
                              return CreateCustomAttributeBuilders(customAttributes).ToArray();
                          });



            foreach (var attribute in customAttributeBuilders)
            {
                propertyBuilder.SetCustomAttribute(attribute);
            }
        }

        private static IEnumerable<CustomAttributeBuilder> CreateCustomAttributeBuilders(IEnumerable<CustomAttributeData> customAttributes)
        {


            var attributeBuilders = customAttributes
                .Select(
                        x =>
                        {
                            var namedArguments = x.NamedArguments;
                            var properties = namedArguments.Select(a => a.MemberInfo).OfType<PropertyInfo>().ToArray();
                            var values = namedArguments.Select(a => a.TypedValue.Value).ToArray();
                            var constructorArgs = x.ConstructorArguments.Select(a => a.Value).ToArray();
                            var constructor = x.Constructor;
                            return new CustomAttributeBuilder(constructor, constructorArgs, properties, values);
                        });
            return attributeBuilders;
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
                    + ":" + GetValueType(field.Value).AssemblyQualifiedName);
        }

        private static string GetTypeName(Type sourceType, Dictionary<string, MemberInfo> fields)
        {
            return fields.Aggregate("LinqConvertTools<>" + sourceType.Name, (current, field) => current + (field.Key + field.Value.MemberType));
        }

        private static Type GetValueType(MemberInfo member)
        {
            return member.MemberType == MemberTypes.Property
                ? ((PropertyInfo)member).PropertyType
                : ((FieldInfo)member).FieldType;
        }
    }
}