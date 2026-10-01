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
        /// Splits a filter into its conditions and the and/or combiners between them, following OData precedence:
        /// <c>not</c> binds tightest, then <c>and</c>, then <c>or</c>, and parentheses override it.
        /// </summary>
        /// <remarks>
        /// A filter with a top-level <c>or</c> yields its or-separated groups; a group that still holds an <c>and</c>
        /// is returned whole, as a token with only <see cref="TokenSet.Left"/>, and is split again when it is parsed.
        /// A filter with only top-level <c>and</c> yields its and-separated operands. A filter with a single top-level
        /// combiner whose first operand is a whole condition, such as <c>(a or b) and c</c>, instead yields one token
        /// that combines the two sides. Each operand is a negation (<c>not</c> and its operand), a comparison, or a
        /// whole condition. A filter that is a single operand yields its negation or comparison, or no tokens when it
        /// is a member, function or literal.
        /// </remarks>
        public static ICollection<TokenSet> GetTokens(this string expression)
        {
            var tokens = new Collection<TokenSet>();
            if (string.IsNullOrWhiteSpace(expression))
            {
                return tokens;
            }

            while (expression.TryGetEnclosedContent(out var content))
            {
                expression = content;
            }

            if (expression.IsImpliedBoolean())
            {
                return tokens;
            }

            var blocks = GetBlocks(expression);
            var topLevel = GetTopLevelFlags(blocks);

            var orIndexes = GetTopLevelIndexes(blocks, topLevel, 0, blocks.Count, TokenOperatorExtensions.IsOrOperation);
            if (orIndexes.Count > 0)
            {
                AddSeparated(tokens, blocks, topLevel, orIndexes, AddOrGroup);
                return tokens;
            }

            var andIndexes = GetTopLevelIndexes(blocks, topLevel, 0, blocks.Count, TokenOperatorExtensions.IsAndOperation);
            if (andIndexes.Count > 0)
            {
                AddSeparated(tokens, blocks, topLevel, andIndexes, AddOperand);
                return tokens;
            }

            var operand = GetOperand(blocks, topLevel, 0, blocks.Count);
            if (!string.IsNullOrWhiteSpace(operand.Operation))
            {
                tokens.Add(operand);
            }

            return tokens;
        }

        /// <summary>
        /// Gets the text inside parentheses that enclose the whole of <paramref name="expression"/>, such as
        /// <c>a eq 1</c> from <c>(a eq 1)</c>, but not from <c>(a eq 1) or (b eq 2)</c>.
        /// Parentheses inside quoted string literals are ignored.
        /// </summary>
        public static bool TryGetEnclosedContent(this string expression, out string content)
        {
            content = string.Empty;
            if (expression.Length < 2 || expression[0] != '(' || expression[expression.Length - 1] != ')')
            {
                return false;
            }

            var depth = 0;
            char? quote = null;
            for (var i = 0; i < expression.Length; i++)
            {
                if (TryAdvanceQuote(expression, ref i, ref quote))
                {
                    continue;
                }

                depth += expression[i] == '(' ? 1 : expression[i] == ')' ? -1 : 0;
                if (depth == 0 && i < expression.Length - 1)
                {
                    return false;
                }
            }

            content = expression.Substring(1, expression.Length - 2);
            return depth == 0;
        }

        public static TokenSet? GetArithmeticToken(this string expression)
        {
            if (expression.TryGetEnclosedContent(out var content))
            {
                expression = content;
            }

            var blocks = expression.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var hasOperation = blocks.Any(x => x.IsArithmetic());
            if (!hasOperation)
            {
                return null;
            }

            var operationIndex = GetArithmeticOperationIndex(blocks);

            var left = string.Join(" ", blocks.Where((x, i) => i < operationIndex));
            var right = string.Join(" ", blocks.Where((x, i) => i > operationIndex));
            var operation = blocks[operationIndex];

            return new TokenSet { Left = left, Operation = operation, Right = right };
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

        private static void AddSeparated(
            Collection<TokenSet> tokens,
            List<string> blocks,
            bool[] topLevel,
            List<int> separatorIndexes,
            Action<Collection<TokenSet>, List<string>, bool[], int, int> addPart)
        {
            if (separatorIndexes.Count == 1 && GetOperand(blocks, topLevel, 0, separatorIndexes[0]).IsWholeCondition)
            {
                var separatorIndex = separatorIndexes[0];
                tokens.Add(new TokenSet
                {
                    Left = JoinBlocks(blocks, 0, separatorIndex),
                    Operation = blocks[separatorIndex].ToLowerInvariant(),
                    Right = JoinBlocks(blocks, separatorIndex + 1, blocks.Count)
                });
                return;
            }

            var start = 0;
            foreach (var separatorIndex in separatorIndexes)
            {
                addPart(tokens, blocks, topLevel, start, separatorIndex);
                tokens.Add(new TokenSet { Operation = blocks[separatorIndex].ToLowerInvariant() });
                start = separatorIndex + 1;
            }

            addPart(tokens, blocks, topLevel, start, blocks.Count);
        }

        private static void AddOrGroup(Collection<TokenSet> tokens, List<string> blocks, bool[] topLevel, int start, int end)
        {
            if (GetTopLevelIndexes(blocks, topLevel, start, end, TokenOperatorExtensions.IsAndOperation).Count > 0)
            {
                tokens.Add(new TokenSet { Left = JoinBlocks(blocks, start, end) });
                return;
            }

            AddOperand(tokens, blocks, topLevel, start, end);
        }

        private static void AddOperand(Collection<TokenSet> tokens, List<string> blocks, bool[] topLevel, int start, int end)
        {
            tokens.Add(GetOperand(blocks, topLevel, start, end));
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

        private static List<int> GetTopLevelIndexes(List<string> blocks, bool[] topLevel, int start, int end, Func<string, bool> predicate)
        {
            var indexes = new List<int>();
            for (var i = start; i < end; i++)
            {
                if (topLevel[i] && predicate(blocks[i]))
                {
                    indexes.Add(i);
                }
            }

            return indexes;
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

        private static string JoinBlocks(List<string> blocks, int start, int end)
        {
            return string.Join(" ", blocks.Skip(start).Take(end - start));
        }

        private static int GetArithmeticOperationIndex(IList<string> blocks)
        {


            var openGroups = 0;
            var operationIndex = -1;
            for (var i = 0; i < blocks.Count; i++)
            {
                var source = blocks[i];



                openGroups += GetNetParentheses(source);

                if (openGroups == 0 && source.IsArithmetic())
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
