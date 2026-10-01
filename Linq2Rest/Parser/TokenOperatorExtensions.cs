// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TokenOperatorExtensions.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the TokenOperatorExtensions type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Parser
{
    using System;
    using System.Text.RegularExpressions;

    internal static class TokenOperatorExtensions
    {
        private static readonly string[] Operations = new[] { "eq", "ne", "gt", "ge", "lt", "le", "and", "or", "not", "in" };
        private const string UnaryOperator = "not";
        private static readonly string[] BinaryCombiners = new[] { "and", "or" };
        private static readonly string[] Arithmetic = new[] { "add", "sub", "mul", "div", "mod" };

        private static readonly string[] BooleanFunctions = new[] { "substringof", "contains", "endswith", "startswith" };
        private static readonly Regex CollectionFunctionRx = new(@"^[0-9a-zA-Z_]+/(all|any)\((.+)\)$", RegexOptions.Compiled, ParserRegex.MatchTimeout);
        private static readonly Regex FunctionRegex = new(@"^([A-Za-z_][A-Za-z0-9_]*)\(.*\)$", RegexOptions.Compiled | RegexOptions.Singleline, ParserRegex.MatchTimeout);
        private static readonly Regex CallRegex = new(@"^[A-Za-z_][A-Za-z0-9_/]*\(.*\)$", RegexOptions.Compiled | RegexOptions.Singleline, ParserRegex.MatchTimeout);

        public static bool IsBinaryCombinationOperation(this string operation)
        {
            return Array.Exists(BinaryCombiners, x => string.Equals(x, operation, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsOrOperation(this string operation)
        {
            return string.Equals(operation, "or", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsUnaryOperation(this string operation)
        {
            return string.Equals(operation, UnaryOperator, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsOperation(this string operation)
        {
            return Array.Exists(Operations, x => string.Equals(x, operation, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsArithmetic(this string operation)
        {
            return Array.Exists(Arithmetic, x => string.Equals(x, operation, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsBooleanFunctionName(string block)
        {
            return Array.Exists(BooleanFunctions, x => block.StartsWith(x, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsCollectionFunction(this string expression)
        {
            return CollectionFunctionRx.IsMatch(expression);
        }

        public static string GetFunctionName(this string expression)
        {
            var functionMatch = FunctionRegex.Match(expression);
            if (functionMatch.Success)
            {
                return functionMatch.Groups[1].Value;
            }

            return string.Empty;
        }

        /// <summary>
        /// Determines whether <paramref name="expression"/> starts with a call such as <c>length(</c> or
        /// <c>Tags/any(</c> and ends with a closing parenthesis.
        /// </summary>
        public static bool IsFunction(this string expression)
        {
            return CallRegex.IsMatch(expression);
        }
    }
}