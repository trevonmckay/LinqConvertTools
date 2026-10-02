// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ParameterValueReader.cs" company="Reimers.dk">
//   Copyright � Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ParameterValueReader type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Parser.Readers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    internal class ParameterValueReader
    {
        private readonly IList<IValueExpressionFactory> _expressionFactories;
        private readonly IValueExpressionFactory[] _customFactories;
        private readonly bool _enumNamesOnly;

        public ParameterValueReader(IEnumerable<IValueExpressionFactory> expressionFactories, bool enumNamesOnly = false)
        {
            _enumNamesOnly = enumNamesOnly;

            // Enum literals are read against the type of the member they are compared with (see GetKnownConstant),
            // so no factory scans assemblies to resolve an enum type from a client-supplied name.
            _customFactories = expressionFactories.ToArray();
            _expressionFactories = _customFactories.Concat(
                new IValueExpressionFactory[]
                {
                    new BooleanExpressionFactory(),
                    new ByteExpressionFactory(),
                    new GuidExpressionFactory(),
                    new DateTimeExpressionFactory(),
                    new TimeSpanExpressionFactory(),
                    new DateTimeOffsetExpressionFactory(),
                    new DecimalExpressionFactory(),
                    new DoubleExpressionFactory(),
                    new SingleExpressionFactory(),
                    new ByteArrayExpressionFactory(),
                    new StreamExpressionFactory(),
                    new LongExpressionFactory(),
                    new IntExpressionFactory(),
                    new ShortExpressionFactory(),
                    new UnsignedIntExpressionFactory(),
                    new UnsignedLongExpressionFactory(),
                    new UnsignedShortExpressionFactory()
                })
                .ToList();
        }

        /// <summary>
        /// Determines whether a factory passed to the constructor handles <paramref name="type"/>.
        /// </summary>
        public bool HasCustomFactory(Type type)
        {
            return Array.Exists(_customFactories, factory => factory.Handles(type));
        }

        public Expression? Read(Type type, string token, IFormatProvider formatProvider)
        {
            var factory = _expressionFactories.FirstOrDefault(x => x.Handles(type));

            return factory == null
                ? GetKnownConstant(type, token, formatProvider)
                : factory.Convert(token);
        }

        private static Expression? GetParseExpression(string filter, IFormatProvider formatProvider, Type type)
        {
            var parseMethods = type.GetMethods(BindingFlags.Static | BindingFlags.Public).Where(x => x.Name == "Parse").ToArray();
            if (parseMethods.Length > 0)
            {
                var withFormatProvider =
                    parseMethods.FirstOrDefault(
                        x =>
                        {
                            var parameters = x.GetParameters();
                            return parameters.Length == 2
                                && typeof(string).IsAssignableFrom(parameters[0].ParameterType)
                                && typeof(IFormatProvider).IsAssignableFrom(parameters[1].ParameterType);
                        });
                if (withFormatProvider != null)
                {
                    return Expression.Call(withFormatProvider, Expression.Constant(filter), Expression.Constant(formatProvider));
                }

                var withoutFormatProvider = parseMethods.FirstOrDefault(
                        x =>
                        {
                            var parameters = x.GetParameters();
                            return parameters.Length == 1
                                && typeof(string).IsAssignableFrom(parameters[0].ParameterType);
                        });

                if (withoutFormatProvider != null)
                {
                    return Expression.Call(withoutFormatProvider, Expression.Constant(filter));
                }
            }

            return null;
        }

        private Expression? GetKnownConstant(Type type, string token, IFormatProvider formatProvider)
        {
            if (type.IsEnum)
            {
                return Expression.Constant(ReadEnum(type, token));
            }

            if (typeof(IConvertible).IsAssignableFrom(type))
            {
                return Expression.Constant(Convert.ChangeType(token, type, formatProvider), type);
            }

            if (type.IsGenericType && typeof(Nullable<>).IsAssignableFrom(type.GetGenericTypeDefinition()))
            {
                if (string.Equals("null", token, StringComparison.OrdinalIgnoreCase))
                {
                    return Expression.Constant(null);
                }

                var genericTypeArgument = type.GetGenericArguments()[0];
                var value = Read(genericTypeArgument, token, formatProvider);
                if (value != null)
                {
                    return Expression.Convert(value, type);
                }
            }

            return GetParseExpression(token, formatProvider, type);
        }

        /// <summary>
        /// Reads an enum literal against the type of the member it is compared with. The literal may be an unqualified
        /// member name (<c>Received</c> or <c>'Received'</c>) or a qualified one (<c>Ns.Type'Received'</c>); a qualifier
        /// is checked against the member's enum type rather than used to look a type up. When the reader is in
        /// names-only mode, a numeric value or a name that is not defined on the type is rejected.
        /// </summary>
        /// <exception cref="FormatException">The qualifier names a different type, or the value is not a member of the enum.</exception>
        private object ReadEnum(Type enumType, string token)
        {
            string member = token;

            // A quoted enum literal ends with its closing quote and the matching open quote is the first occurrence
            // of that same character, so the delimiter is chosen by position rather than by which quote character
            // happens to appear first in the token.
            if (token.Length > 0 && (token[token.Length - 1] == '\'' || token[token.Length - 1] == '"'))
            {
                char quote = token[token.Length - 1];
                int firstQuote = token.IndexOf(quote);
                int lastQuote = token.Length - 1;
                if (lastQuote <= firstQuote)
                {
                    throw new FormatException("Could not read " + token + " as " + enumType.Name + ".");
                }

                string qualifier = token.Substring(0, firstQuote);
                if (qualifier.Length > 0 && !QualifierMatchesType(qualifier, enumType))
                {
                    throw new FormatException("The enum type '" + qualifier + "' in " + token + " is not " + enumType.Name + ".");
                }

                member = token.Substring(firstQuote + 1, lastQuote - firstQuote - 1);
            }

            if (_enumNamesOnly)
            {
                string[] names = Enum.GetNames(enumType);
                string[] parts = member.Split(',');

                // A comma-combined value only makes sense for a [Flags] enum; on any other enum Enum.Parse would
                // still OR the members together and silently yield a value equal to some unrelated member.
                if (parts.Length > 1 && !enumType.IsDefined(typeof(FlagsAttribute), false))
                {
                    throw new FormatException("'" + member + "' combines members, but " + enumType.Name + " is not a [Flags] enum.");
                }

                foreach (string part in parts)
                {
                    string name = part.Trim();
                    if (name.Length == 0 || long.TryParse(name, out _) || !Array.Exists(names, n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new FormatException(NotAMemberMessage(name, enumType));
                    }
                }
            }

            try
            {
                return Enum.Parse(enumType, member, true);
            }
            catch (ArgumentException)
            {
                throw new FormatException(NotAMemberMessage(member, enumType));
            }
        }

        /// <summary>
        /// Builds the message for a value that is not a member of <paramref name="enumType"/>. It names the type by its
        /// short name and lists the defined members, so a caller that surfaces the message (for example as an HTTP 400)
        /// shows the allowed values without the type's namespace.
        /// </summary>
        private static string NotAMemberMessage(string value, Type enumType)
        {
            return "'" + value + "' is not a member of " + enumType.Name + ". Members: " + string.Join(", ", Enum.GetNames(enumType)) + ".";
        }

        /// <summary>
        /// Matches a qualifier in a qualified enum literal against the enum type, case-insensitively like the member
        /// name. A nested type's <see cref="Type.FullName"/> joins the nesting with <c>+</c> while a filter usually
        /// writes it with <c>.</c>, so the full name is compared with the separators normalized; the short name is
        /// also accepted.
        /// </summary>
        private static bool QualifierMatchesType(string qualifier, Type enumType)
        {
            return string.Equals(qualifier, enumType.Name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(qualifier.Replace('+', '.'), (enumType.FullName ?? string.Empty).Replace('+', '.'), StringComparison.OrdinalIgnoreCase);
        }

        [ContractInvariantMethod]
        private void Invariants()
        {

        }
    }
}