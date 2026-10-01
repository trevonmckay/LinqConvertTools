// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ExpressionTokenizer.cs" company="Reimers.dk">
//   Copyright © Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the ExpressionTokenizer type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Text.RegularExpressions;

    internal static class ExpressionTokenizer
    {
        private static readonly Regex FunctionRx = new Regex(@"^([^\(\)]+)\((.+)\)$", RegexOptions.Compiled, ParserRegex.MatchTimeout);
        private static readonly Regex FunctionContentRx = new Regex(@"^(.*\((?>[^()]+|\((?<Depth>.*)|\)(?<-Depth>.*))*(?(Depth)(?!))\)|.*?)\s*,\s*(((?<Open>').*(?<Close-Open>')(?(Open)(?!)))|[^,]*)$", RegexOptions.Compiled, ParserRegex.MatchTimeout);
        private static readonly Regex AnyAllFunctionRx = new Regex(@"^(([0-9a-zA-Z_]+/)+)(any|all)\((.*)\)$", RegexOptions.Compiled, ParserRegex.MatchTimeout);

        /// <summary>
        /// Splits a filter into its conditions and the and/or combiners between them.
        /// </summary>
        /// <remarks>
        /// The conditions and combiners are returned in order and are combined by OData precedence when parsed:
        /// <c>not</c> binds tightest, then <c>and</c>, then <c>or</c>, and parentheses override it. Each condition is a
        /// negation (<c>not</c> and its operand), a comparison, or a whole condition such as a group in parentheses.
        /// A filter with a single combiner whose first operand is a whole condition, such as <c>(a or b) and c</c>,
        /// instead yields one token that combines the two sides. A filter that is a single condition yields its
        /// negation or comparison, or no tokens when it is a member, function or literal.
        /// Parentheses that enclose the whole filter are expected to be removed first, with
        /// <see cref="StripEnclosingParentheses"/>.
        /// </remarks>
        public static ICollection<TokenSet> GetTokens(this string expression)
        {
            var tokens = new Collection<TokenSet>();
            if (string.IsNullOrWhiteSpace(expression))
            {
                return tokens;
            }

            var blocks = GetBlocks(expression);
            if (IsImpliedBoolean(expression, blocks))
            {
                return tokens;
            }

            var topLevel = GetTopLevelFlags(blocks);
            var combinerIndexes = new List<int>();
            for (var i = 0; i < blocks.Count; i++)
            {
                if (topLevel[i] && blocks[i].IsBinaryCombinationOperation())
                {
                    combinerIndexes.Add(i);
                }
            }

            var firstOperandEnd = combinerIndexes.Count > 0 ? combinerIndexes[0] : blocks.Count;
            var firstOperand = GetOperand(blocks, topLevel, 0, firstOperandEnd);
            if (combinerIndexes.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(firstOperand.Operation))
                {
                    tokens.Add(firstOperand);
                }

                return tokens;
            }

            if (combinerIndexes.Count == 1 && firstOperand.IsWholeCondition)
            {
                tokens.Add(new TokenSet
                {
                    Left = firstOperand.Left,
                    Operation = blocks[firstOperandEnd].ToLowerInvariant(),
                    Right = JoinBlocks(blocks, firstOperandEnd + 1, blocks.Count)
                });
                return tokens;
            }

            tokens.Add(firstOperand);
            for (var c = 0; c < combinerIndexes.Count; c++)
            {
                var combinerIndex = combinerIndexes[c];
                var operandEnd = c + 1 < combinerIndexes.Count ? combinerIndexes[c + 1] : blocks.Count;
                tokens.Add(new TokenSet { Operation = blocks[combinerIndex].ToLowerInvariant() });
                tokens.Add(GetOperand(blocks, topLevel, combinerIndex + 1, operandEnd));
            }

            return tokens;
        }

        /// <summary>
        /// Removes parentheses that enclose the whole of <paramref name="expression"/>, at any depth, such as
        /// <c>((a eq 1))</c> to <c>a eq 1</c>, but not those of <c>(a eq 1) or (b eq 2)</c>. Parentheses inside quoted
        /// string literals are ignored, and an expression with unbalanced parentheses is returned unchanged.
        /// </summary>
        public static string StripEnclosingParentheses(this string expression)
        {
            var start = 0;
            var end = expression.Length - 1;
            SkipWhiteSpace(expression, ref start, ref end);
            if (start >= end || expression[start] != '(' || expression[end] != ')')
            {
                return expression;
            }

            var closeIndexes = GetCloseIndexes(expression);
            if (closeIndexes is null)
            {
                return expression;
            }

            var stripped = false;
            while (start < end && expression[start] == '(' && closeIndexes[start] == end)
            {
                start++;
                end--;
                SkipWhiteSpace(expression, ref start, ref end);
                stripped = true;
            }

            return stripped ? expression.Substring(start, end - start + 1) : expression;
        }

        /// <summary>
        /// Splits an arithmetic expression at its last top-level arithmetic operator.
        /// </summary>
        /// <returns>The operands and operator, or <c>null</c> when no arithmetic operator is outside parentheses, such
        /// as in <c>round(Price mul 2)</c>.</returns>
        public static TokenSet? GetArithmeticToken(this string expression)
        {
            expression = expression.StripEnclosingParentheses();

            var blocks = GetBlocks(expression);
            var operationIndex = GetArithmeticOperationIndex(blocks);
            if (operationIndex < 0)
            {
                return null;
            }

            return new TokenSet
            {
                Left = JoinBlocks(blocks, 0, operationIndex),
                Operation = blocks[operationIndex],
                Right = JoinBlocks(blocks, operationIndex + 1, blocks.Count)
            };
        }

        public static TokenSet? GetAnyAllFunctionTokens(this string filter)
        {
            var functionMatch = AnyAllFunctionRx.Match(filter);
            if (!functionMatch.Success)
            {
                return null;
            }

            var functionCollection = functionMatch.Groups[1].Value.Trim('/');
            var functionName = functionMatch.Groups[3].Value;
            var functionContent = functionMatch.Groups[4].Value;

            return new FunctionTokenSet
            {
                Operation = functionName,
                Left = functionCollection,
                Right = functionContent
            };
        }

        public static TokenSet? GetFunctionTokens(this string filter)
        {
            var functionMatch = FunctionRx.Match(filter);
            if (!functionMatch.Success)
            {
                return null;
            }

            var functionName = functionMatch.Groups[1].Value;
            var functionContent = functionMatch.Groups[2].Value;
            var functionContentMatch = FunctionContentRx.Match(functionContent);
            if (!functionContentMatch.Success)
            {
                return new FunctionTokenSet
                {
                    Operation = functionName,
                    Left = functionContent
                };
            }

            return new FunctionTokenSet
            {
                Operation = functionName,
                Left = functionContentMatch.Groups[1].Value,
                Right = functionContentMatch.Groups[2].Value
            };
        }

        /// <summary>
        /// Determines whether <paramref name="expression"/> is a call to a boolean string function or a collection
        /// <c>any</c>/<c>all</c>, and nothing else, so it needs no splitting into conditions.
        /// </summary>
        private static bool IsImpliedBoolean(string expression, List<string> blocks)
        {
            return expression.IsFunction()
                && !blocks.Exists(TokenOperatorExtensions.IsOperation)
                && (TokenOperatorExtensions.IsBooleanFunctionName(blocks[0]) || expression.IsCollectionFunction());
        }

        /// <summary>
        /// Reads the operand in <paramref name="blocks"/> from <paramref name="start"/> up to <paramref name="end"/>,
        /// which holds no top-level and/or: a negation, a comparison at its first top-level operator, or a whole
        /// condition. An empty range gives an empty token, which the parser rejects as a misplaced combiner.
        /// </summary>
        private static TokenSet GetOperand(List<string> blocks, bool[] topLevel, int start, int end)
        {
            if (start >= end)
            {
                return new TokenSet();
            }

            if (blocks[start].IsUnaryOperation())
            {
                return new TokenSet { Operation = blocks[start], Right = JoinBlocks(blocks, start + 1, end) };
            }

            for (var i = start; i < end; i++)
            {
                if (topLevel[i] && blocks[i].IsOperation())
                {
                    return new TokenSet
                    {
                        Left = JoinBlocks(blocks, start, i),
                        Operation = blocks[i],
                        Right = JoinBlocks(blocks, i + 1, end)
                    };
                }
            }

            return new TokenSet { Left = JoinBlocks(blocks, start, end) };
        }

        /// <summary>
        /// Marks each block that ends outside all parentheses. Operators hold no parentheses, so an operator marked
        /// this way is not enclosed in any group.
        /// </summary>
        private static bool[] GetTopLevelFlags(List<string> blocks)
        {
            var flags = new bool[blocks.Count];
            var depth = 0;
            for (var i = 0; i < blocks.Count; i++)
            {
                depth += GetNetParentheses(blocks[i]);
                flags[i] = depth == 0;
            }

            return flags;
        }

        /// <summary>
        /// Counts opening minus closing parentheses in <paramref name="block"/>, ignoring any inside quoted string
        /// literals.
        /// </summary>
        private static int GetNetParentheses(string block)
        {
            var net = 0;
            char? quote = null;
            for (var i = 0; i < block.Length; i++)
            {
                if (TryAdvanceQuote(block, ref i, ref quote))
                {
                    continue;
                }

                net += block[i] == '(' ? 1 : block[i] == ')' ? -1 : 0;
            }

            return net;
        }

        /// <summary>
        /// Tracks whether <paramref name="index"/> is inside a string literal quoted with ' or ". A doubled quote
        /// inside a literal is an escaped quote and is skipped over.
        /// </summary>
        /// <returns><c>true</c> when the character at <paramref name="index"/> is part of a string literal.</returns>
        private static bool TryAdvanceQuote(string text, ref int index, ref char? quote)
        {
            var c = text[index];
            if (quote is null)
            {
                if (c != '\'' && c != '"')
                {
                    return false;
                }

                quote = c;
                return true;
            }

            if (c == quote)
            {
                if (index + 1 < text.Length && text[index + 1] == quote)
                {
                    index++;
                }
                else
                {
                    quote = null;
                }
            }

            return true;
        }

        /// <summary>
        /// Finds the index of the closing parenthesis that matches each opening one, ignoring parentheses inside quoted
        /// string literals.
        /// </summary>
        /// <returns>The closing index at each opening index, or <c>null</c> when the parentheses are unbalanced.</returns>
        private static int[]? GetCloseIndexes(string expression)
        {
            var closeIndexes = new int[expression.Length];
            var openIndexes = new Stack<int>();
            char? quote = null;
            for (var i = 0; i < expression.Length; i++)
            {
                if (TryAdvanceQuote(expression, ref i, ref quote))
                {
                    continue;
                }

                if (expression[i] == '(')
                {
                    openIndexes.Push(i);
                }
                else if (expression[i] == ')')
                {
                    if (openIndexes.Count == 0)
                    {
                        return null;
                    }

                    closeIndexes[openIndexes.Pop()] = i;
                }
            }

            return openIndexes.Count == 0 ? closeIndexes : null;
        }

        private static void SkipWhiteSpace(string expression, ref int start, ref int end)
        {
            while (start <= end && char.IsWhiteSpace(expression[start]))
            {
                start++;
            }

            while (end >= start && char.IsWhiteSpace(expression[end]))
            {
                end--;
            }
        }

        private static string JoinBlocks(List<string> blocks, int start, int end)
        {
            return string.Join(" ", blocks.Skip(start).Take(end - start));
        }

        private static int GetArithmeticOperationIndex(List<string> blocks)
        {
            var operationIndex = -1;
            var depth = 0;
            for (var i = 0; i < blocks.Count; i++)
            {
                depth += GetNetParentheses(blocks[i]);
                if (depth == 0 && blocks[i].IsArithmetic())
                {
                    operationIndex = i;
                }
            }

            return operationIndex;
        }

        /// <summary>
        /// Splits <paramref name="str"/> by spaces that are not inside a string literal quoted with ' or ".
        /// Empty blocks are excluded from the returned list.
        /// </summary>
        private static List<string> GetBlocks(string str)
        {
            var blocks = new List<string>();
            var blockStart = 0;
            char? quote = null;
            for (var pos = 0; pos < str.Length; pos++)
            {
                if (TryAdvanceQuote(str, ref pos, ref quote) || str[pos] != ' ')
                {
                    continue;
                }

                if (pos > blockStart)
                {
                    blocks.Add(str.Substring(blockStart, pos - blockStart));
                }

                blockStart = pos + 1;
            }

            if (blockStart < str.Length)
            {
                blocks.Add(str.Substring(blockStart));
            }

            return blocks;
        }
    }
}
