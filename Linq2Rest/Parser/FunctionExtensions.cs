// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FunctionExtensions.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the FunctionExtensions type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Parser
{
    using System;
    using System.Collections.Generic;

    internal static class FunctionExtensions
    {
        private static readonly Dictionary<string, (Type Type, int Arguments)> KnownFunctions = new(StringComparer.OrdinalIgnoreCase)
                                                                              {
                                                                                  { "length", (typeof(int), 1) },
                                                                                  { "substring", (typeof(string), 2) },
                                                                                  { "substringof", (typeof(bool), 2) },
                                                                                  { "contains", (typeof(bool), 2) },
                                                                                  { "endswith", (typeof(bool), 2) },
                                                                                  { "startswith", (typeof(bool), 2) },
                                                                                  { "indexof", (typeof(int), 2) },
                                                                                  { "tolower", (typeof(string), 1) },
                                                                                  { "toupper", (typeof(string), 1) },
                                                                                  { "trim", (typeof(string), 1) },
                                                                                  { "year", (typeof(int), 1) },
                                                                                  { "month", (typeof(int), 1) },
                                                                                  { "day", (typeof(int), 1) },
                                                                                  { "hour", (typeof(int), 1) },
                                                                                  { "minute", (typeof(int), 1) },
                                                                                  { "second", (typeof(int), 1) },
                                                                                  { "floor", (typeof(int), 1) },
                                                                                  { "ceiling", (typeof(int), 1) },
                                                                                  { "round", (typeof(double), 1) }
                                                                              };

        public static Type? GetFunctionType(this string functionName)
        {
            return KnownFunctions.TryGetValue(functionName, out var function) ? function.Type : null;
        }

        /// <summary>
        /// Gets the number of arguments a known function takes.
        /// </summary>
        /// <returns>The number of arguments, or <c>null</c> when <paramref name="functionName"/> is not a known function.</returns>
        public static int? GetFunctionArgumentCount(this string functionName)
        {
            return KnownFunctions.TryGetValue(functionName, out var function) ? function.Arguments : null;
        }
    }
}