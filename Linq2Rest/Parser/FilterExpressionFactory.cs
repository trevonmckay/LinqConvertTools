// --------------------------------------------------------------------------------------------------------------------
// <copyright file="FilterExpressionFactory.cs" company="Reimers.dk">
//   Copyright � Reimers.dk 2014
//   This source is subject to the Microsoft Public License (Ms-PL).
//   Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
//   All other rights reserved.
// </copyright>
// <summary>
//   Defines the FilterExpressionFactory.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace LinqConvertTools.Parser
{
    using LinqConvertTools.Parser.Readers;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Defines the FilterExpressionFactory.
    /// </summary>
    public class FilterExpressionFactory : IFilterExpressionFactory
    {
        private static readonly char[] LiteralCharacters = { '\'', '"', '(', ')' };
        private static readonly Regex NegateRx = new(@"^-[^\d]*", RegexOptions.Compiled, ParserRegex.MatchTimeout);
        private static readonly Expression _nullConstantExpression = Expression.Constant(null, typeof(object));

        /// <summary>
        /// The default for <see cref="MaxDepth"/>.
        /// </summary>
        public const int DefaultMaxDepth = 256;

        private readonly IMemberNameResolver _memberNameResolver;
        private readonly ParameterValueReader _valueReader;
        private readonly StringCaseFolding _caseFolding;
        private readonly MethodInfo _toUpperMethod;
        private readonly MethodInfo _toLowerMethod;
        private int _maxDepth = DefaultMaxDepth;

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterExpressionFactory"/> class that converts string case
        /// with <see cref="StringCaseFolding.CurrentCulture"/>.
        /// </summary>
        /// <param name="memberNameResolver">An <see cref="IMemberNameResolver"/> for name resolution.</param>
        /// <param name="expressionFactories">The custom <see cref="IValueExpressionFactory"/> to use for value conversion.</param>
        public FilterExpressionFactory(IMemberNameResolver memberNameResolver, IEnumerable<IValueExpressionFactory> expressionFactories)
            : this(memberNameResolver, expressionFactories, StringCaseFolding.CurrentCulture)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterExpressionFactory"/> class.
        /// </summary>
        /// <param name="memberNameResolver">An <see cref="IMemberNameResolver"/> for name resolution.</param>
        /// <param name="expressionFactories">The custom <see cref="IValueExpressionFactory"/> to use for value conversion.</param>
        /// <param name="caseFolding">The string methods used for case-insensitive comparisons and the <c>toupper()</c> and <c>tolower()</c> functions.</param>
        public FilterExpressionFactory(IMemberNameResolver memberNameResolver, IEnumerable<IValueExpressionFactory> expressionFactories, StringCaseFolding caseFolding)
            : this(memberNameResolver, expressionFactories, caseFolding, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FilterExpressionFactory"/> class.
        /// </summary>
        /// <param name="memberNameResolver">An <see cref="IMemberNameResolver"/> for name resolution.</param>
        /// <param name="expressionFactories">The custom <see cref="IValueExpressionFactory"/> to use for value conversion.</param>
        /// <param name="caseFolding">The string methods used for case-insensitive comparisons and the <c>toupper()</c> and <c>tolower()</c> functions.</param>
        /// <param name="enumNamesOnly">When <c>true</c>, an enum literal is read only as a defined member name; a numeric value or an undefined name is rejected.</param>
        public FilterExpressionFactory(IMemberNameResolver memberNameResolver, IEnumerable<IValueExpressionFactory> expressionFactories, StringCaseFolding caseFolding, bool enumNamesOnly)
        {
            if (!Enum.IsDefined(typeof(StringCaseFolding), caseFolding))
            {
                throw new ArgumentOutOfRangeException(nameof(caseFolding), caseFolding, "Unknown string case folding.");
            }

            _valueReader = new ParameterValueReader(expressionFactories, enumNamesOnly);
            _memberNameResolver = memberNameResolver;
            _caseFolding = caseFolding;
            _toUpperMethod = MethodProvider.GetToUpperMethod(caseFolding);
            _toLowerMethod = MethodProvider.GetToLowerMethod(caseFolding);
        }

        /// <summary>
        /// Gets or sets the deepest a parsed filter may nest, counted in levels of the expression tree it
        /// produces. A filter nested deeper, or a chain of conditions longer, throws
        /// <see cref="InvalidOperationException"/>. Code that walks the returned expression, such as a query
        /// provider, typically recurses once per level, so this bounds the stack that code needs.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
        public int MaxDepth
        {
            get => _maxDepth;
            set => _maxDepth = value >= 1
                ? value
                : throw new ArgumentOutOfRangeException(nameof(value), value, "The maximum depth must be at least 1.");
        }

        /// <summary>
        /// Creates a filter expression from its string representation.
        /// </summary>
        /// <param name="filter">The string representation of the filter.</param>
        /// <param name="ignoreCase">When true the returned expression ensures string case is ignored.</param>
        /// <typeparam name="T">The <see cref="Type"/> of item to filter.</typeparam>
        /// <returns>An <see cref="Expression{TDelegate}"/> if the passed filter is valid, otherwise null.</returns>
        public Expression<Func<T, bool>> Create<T>(string filter, bool ignoreCase = false)
        {
            return Create<T>(filter, CultureInfo.InvariantCulture, ignoreCase);
        }

        /// <summary>
        /// Creates a filter expression from its string representation.
        /// </summary>
        /// <param name="filter">The string representation of the filter.</param>
        /// <param name="formatProvider">The <see cref="IFormatProvider"/> to use when reading the filter.</param>
        /// <param name="ignoreCase">When true the returned expression ensures string case is ignored.</param>
        /// <typeparam name="T">The <see cref="Type"/> of item to filter.</typeparam>
        /// <returns>An <see cref="Expression{TDelegate}"/> if the passed filter is valid, otherwise null.</returns>
        public Expression<Func<T, bool>> Create<T>(string filter, IFormatProvider formatProvider, bool ignoreCase = false)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                return x => true;
            }

            var parameter = Expression.Parameter(typeof(T), "x");

            Expression? expression = CreateExpression<T>(filter, parameter, new List<ParameterExpression>(), null, formatProvider, ignoreCase, 1);
            if (expression is null)
            {
                throw new InvalidOperationException("Could not create valid expression from: " + filter);
            }

            // A chain of conditions is built in a loop rather than by recursion, so the parser's own depth
            // check does not bound it. The finished tree is measured instead.
            if (ExpressionDepth.Exceeds(expression, _maxDepth))
            {
                throw DepthExceeded();
            }

            return Expression.Lambda<Func<T, bool>>(expression, parameter);
        }

        private static Type? GetFunctionParameterType(string operation)
        {
            switch (operation.ToUpperInvariant())
            {
                case "SUBSTRING":
                    return typeof(int);
                default:
                    return null;
            }
        }

        private Expression GetOperation(string token, Expression? left, Expression right, bool ignoreCase)
        {
            return left == null ? GetRightOperation(token, right) : GetNullSafeLeftRightOperation(token, left, right, ignoreCase);
        }

        private Expression GetNullSafeLeftRightOperation(string token, Expression left, Expression right, bool ignoreCase)
        {
            if (token.IsBinaryCombinationOperation())
            {
                left = GetNullSafeCondition(left);
                right = GetNullSafeCondition(right);
            }

            Expression binaryExpression = GetLeftRightOperation(token, left, right, ignoreCase);
            if (left is MemberExpression memberExpression && memberExpression.Expression?.NodeType == ExpressionType.MemberAccess && !memberExpression.Expression.Type.IsValueType)
            {
                return Expression.AndAlso(Expression.NotEqual(_nullConstantExpression, memberExpression.Expression), binaryExpression);
            }
            else if (left is MethodCallExpression methodCallExpression && methodCallExpression.Object?.NodeType == ExpressionType.MemberAccess)
            {
                return Expression.AndAlso(Expression.NotEqual(_nullConstantExpression, methodCallExpression.Object), binaryExpression);
            }

            return binaryExpression;
        }

        /// <summary>
        /// Guards a condition that reads a member of a nested object, such as <c>x.Child.Active</c>, so that it is
        /// false when the nested object is null, instead of throwing or deciding the conditions combined with it.
        /// </summary>
        private static Expression GetNullSafeCondition(Expression condition)
        {
            return condition is MemberExpression memberExpression && memberExpression.Expression?.NodeType == ExpressionType.MemberAccess && !memberExpression.Expression.Type.IsValueType
                ? Expression.AndAlso(Expression.NotEqual(_nullConstantExpression, memberExpression.Expression), condition)
                : condition;
        }

        private Expression GetCaseAwareLeftRightOperation(BinaryExpression binaryExpression, bool ignoreCase)
        {
            if (!ignoreCase || binaryExpression.Left.Type != typeof(string))
            {
                return binaryExpression;
            }

            Expression left = FoldCase(binaryExpression.Left);
            Expression right = FoldCase(binaryExpression.Right);
            return binaryExpression.Update(left, binaryExpression.Conversion, right);
        }

        /// <summary>
        /// Upper-cases a string operand for a case-insensitive comparison. A null constant has no case and is compared
        /// as-is, so a comparison with <c>null</c> does not try to upper-case it. A value read at query time is kept
        /// null when it is null, instead of throwing when the filter runs in memory; a query provider translates
        /// <c>UPPER(null)</c> to null too, so the rows are the same.
        /// </summary>
        private Expression FoldCase(Expression operand)
        {
            if (operand is ConstantExpression constant)
            {
                return constant.Value is string value
                    ? Expression.Constant(ToUpper(value), typeof(string))
                    : operand;
            }

            Expression nullString = Expression.Constant(null, typeof(string));
            return Expression.Condition(Expression.Equal(operand, nullString), nullString, Expression.Call(operand, _toUpperMethod));
        }

        private Expression GetUpperCaseList(Expression expression)
        {
            if (expression is ConstantExpression { Value: string?[] values })
            {
                return Expression.Constant(values.Select(value => value is null ? null : ToUpper(value)).ToArray());
            }

            // A collection member, such as x.Tags, is upper-cased element by element at query time, so the lookup is
            // case-insensitive on both sides. Each element is folded the same way as a single value.
            if (typeof(IEnumerable<string>).IsAssignableFrom(expression.Type))
            {
                ParameterExpression element = Expression.Parameter(typeof(string), "x");
                return Expression.Call(typeof(Enumerable), nameof(Enumerable.Select), new[] { typeof(string), typeof(string) }, expression, Expression.Lambda(FoldCase(element), element));
            }

            return expression;
        }

        private static bool IsInOperation(string operation)
        {
            return string.Equals(operation, "in", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads the right side of <c>in</c>: a list of literals in parentheses, or else a collection member such as
        /// <c>Tags</c>, or a single literal.
        /// </summary>
        /// <exception cref="FormatException">The parentheses do not enclose the whole list, as in <c>(1), (3)</c>.</exception>
        private Expression GetInListOperand<T>(string operand, Type memberType, ParameterExpression parameter, ICollection<ParameterExpression> lambdaParameters, IFormatProvider formatProvider, int depth)
        {
            operand = operand.Trim();
            if (operand.StartsWith("(", StringComparison.Ordinal))
            {
                string items = operand.StripEnclosingParentheses(out int levels);
                return levels > 0
                    ? GetTypedInList(items, memberType, formatProvider)
                    : throw new FormatException("Could not read " + operand + " as an in list.");
            }

            Expression? collection = GetPropertyExpression<T>(operand, parameter, lambdaParameters, depth + 1);
            bool isCollection = collection is not null
                && collection.Type != typeof(string)
                && typeof(IEnumerable<>).MakeGenericType(memberType).IsAssignableFrom(collection.Type);
            return isCollection ? collection! : GetTypedInList(operand, memberType, formatProvider);
        }

        /// <summary>
        /// Reads each item of an <c>in</c> list as a literal of the member's type, so the list becomes an array the
        /// member's type can be looked up in.
        /// </summary>
        /// <param name="list">The items, separated by commas, without the parentheses around the list.</param>
        /// <param name="memberType">The type of the member looked up in the list.</param>
        /// <param name="formatProvider">The format provider to read items with.</param>
        /// <exception cref="FormatException">An item is not a literal of the member's type.</exception>
        private ConstantExpression GetTypedInList(string list, Type memberType, IFormatProvider formatProvider)
        {
            string[] items = list
                .SplitTopLevel()
                .Select(item => item.Trim().StripEnclosingParentheses())
                .Where(item => item.Length > 0)
                .ToArray();
            Array values = Array.CreateInstance(memberType, items.Length);
            for (int i = 0; i < items.Length; i++)
            {
                values.SetValue(ReadInListItem(items[i], memberType, formatProvider), i);
            }

            return Expression.Constant(values);
        }

        private object? ReadInListItem(string item, Type memberType, IFormatProvider formatProvider)
        {
            Type? underlyingType = Nullable.GetUnderlyingType(memberType);
            if (memberType == typeof(string) && !string.Equals(item, "null", StringComparison.OrdinalIgnoreCase))
            {
                if (TryReadStringLiteral(item, out string? literal))
                {
                    return literal;
                }

                // An unquoted item is read as its text. A quote or parenthesis in it means a malformed literal.
                return item.IndexOfAny(LiteralCharacters) < 0
                    ? item
                    : throw new FormatException("Could not read " + item + " as a string literal.");
            }

            if (string.Equals(item, "null", StringComparison.OrdinalIgnoreCase))
            {
                return !memberType.IsValueType || underlyingType is not null
                    ? null
                    : throw new FormatException("Could not read null as " + memberType.Name + ".");
            }

            Expression value = _valueReader.Read(underlyingType ?? memberType, item, formatProvider)
                ?? throw new FormatException("Could not read " + item + " as " + memberType.Name + ".");

            // A type without a dedicated reader is read as a call to its Parse method, which is evaluated here so the
            // list stays a constant a query provider can translate.
            return value is ConstantExpression constant
                ? constant.Value
                : Expression.Lambda(value).Compile().DynamicInvoke();
        }

        /// <summary>
        /// Converts a filter constant to upper case the same way the generated expression converts the member,
        /// so both sides of a case-insensitive comparison use the same rules.
        /// </summary>
        private string ToUpper(string value)
        {
            return _caseFolding == StringCaseFolding.Invariant
                ? value.ToUpperInvariant()
                : value.ToUpper(CultureInfo.CurrentCulture);
        }

        private Expression GetInOperation(Expression left, Expression right, bool ignoreCase)
        {
            if (!ignoreCase || left.Type != typeof(string))
            {
                return Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), new[] { left.Type }, right, left);
            }

            Expression contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), new[] { left.Type }, GetUpperCaseList(right), Expression.Call(left, _toUpperMethod));
            Expression nonNullMatch = Expression.AndAlso(Expression.NotEqual(left, _nullConstantExpression), contains);
            return right is ConstantExpression { Value: string?[] values } && values.Contains(null)
                ? Expression.OrElse(Expression.Equal(left, _nullConstantExpression), nonNullMatch)
                : nonNullMatch;
        }

        private Expression GetLeftRightOperation(string token, Expression left, Expression right, bool ignoreCase)
        {
            string operation = token.ToUpperInvariant();
            if (operation != "IN")
            {
                LiftToNullable(ref left, ref right);
            }

            switch (operation)
            {
                case "EQ":
                    if (left.Type.IsEnum && left.Type.GetCustomAttributes(typeof(FlagsAttribute), true).Any())
                    {
                        var underlyingType = Enum.GetUnderlyingType(left.Type);
                        var leftValue = Expression.Convert(left, underlyingType);
                        var rightValue = Expression.Convert(right, underlyingType);
                        var andExpression = Expression.And(leftValue, rightValue);
                        return Expression.Equal(andExpression, rightValue);
                    }
                    return GetCaseAwareLeftRightOperation(Expression.Equal(left, right), ignoreCase);
                case "NE":
                    return GetCaseAwareLeftRightOperation(Expression.NotEqual(left, right), ignoreCase);
                case "GT":
                    return GetCaseAwareLeftRightOperation(Expression.GreaterThan(left, right), ignoreCase);
                case "GE":
                    return GetCaseAwareLeftRightOperation(Expression.GreaterThanOrEqual(left, right), ignoreCase);
                case "LT":
                    return GetCaseAwareLeftRightOperation(Expression.LessThan(left, right), ignoreCase);
                case "LE":
                    return GetCaseAwareLeftRightOperation(Expression.LessThanOrEqual(left, right), ignoreCase);
                case "AND":
                    return Expression.AndAlso(left, right);
                case "OR":
                    return Expression.OrElse(left, right);
                case "IN":
                    return GetInOperation(left, right, ignoreCase);
                case "ADD":
                    return Expression.Add(left, right);
                case "SUB":
                    return Expression.Subtract(left, right);
                case "MUL":
                    return Expression.Multiply(left, right);
                case "DIV":
                    return Expression.Divide(left, right);
                case "MOD":
                    return Expression.Modulo(left, right);
            }

            throw new InvalidOperationException("Could not understand operation: " + token);
        }

        private static Expression GetRightOperation(string token, Expression right)
        {
            Expression? result = null;
            switch (token.ToUpperInvariant())
            {
                case "NOT":
                    result = right.Type == typeof(bool) ? Expression.Not(GetNullSafeCondition(right)) : null;
                    break;
            }

            if (result is null)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Could not create valid expression from: {0} {1}", token, right));
            }

            return result;
        }

        /// <remarks>
        /// <paramref name="instance"/> and the elements of <paramref name="parameters"/> are null when the filter omits a function argument.
        /// <see cref="Expression.Call(Expression, MethodInfo, Expression[])"/> rejects a missing argument with an <see cref="ArgumentException"/>.
        /// </remarks>
        private Expression? GetCaseAwareFunction(Expression? instance, MethodInfo method, Expression?[] parameters, bool ignoreCase)
        {
            Expression? innerInstance = ignoreCase ? Expression.Call(instance, _toUpperMethod) : instance;
            Expression?[] innerParameters = ignoreCase ? parameters.Select(x => (Expression?)Expression.Call(x, _toUpperMethod)).ToArray() : parameters;

            return Expression.Call(innerInstance, method, innerParameters);
        }

        private Expression? GetFunction(string function, Expression left, Expression? right, ParameterExpression sourceParameter, ICollection<ParameterExpression> lambdaParameters, bool ignoreCase)
        {
            switch (function.ToUpperInvariant())
            {
                case "SUBSTRINGOF":
                    return GetCaseAwareFunction(right, MethodProvider.ContainsMethod, new[] { left }, ignoreCase);
                case "CONTAINS":
                    return Expression.AndAlso(Expression.NotEqual(left, _nullConstantExpression), GetCaseAwareFunction(left, MethodProvider.ContainsMethod, new[] { right }, ignoreCase));
                case "ENDSWITH":
                    return Expression.AndAlso(Expression.NotEqual(left, _nullConstantExpression), GetCaseAwareFunction(left, MethodProvider.EndsWithMethod, new[] { right }, ignoreCase));
                case "STARTSWITH":
                    return Expression.AndAlso(Expression.NotEqual(left, _nullConstantExpression), GetCaseAwareFunction(left, MethodProvider.StartsWithMethod, new[] { right }, ignoreCase));
                case "LENGTH":
                    return Expression.Property(left, MethodProvider.LengthProperty);
                case "INDEXOF":
                    return GetCaseAwareFunction(left, MethodProvider.IndexOfMethod, new[] { right }, ignoreCase);
                case "SUBSTRING":
                    return Expression.Call(left, MethodProvider.SubstringMethod, new[] { right });
                case "TOLOWER":
                    return Expression.Call(left, _toLowerMethod);
                case "TOUPPER":
                    return Expression.Call(left, _toUpperMethod);
                case "TRIM":
                    return Expression.Call(left, MethodProvider.TrimMethod);
                case "HOUR":
                case "MINUTE":
                case "SECOND":
                case "DAY":
                case "MONTH":
                case "YEAR":
                    return GetDatePart(left, function);
                case "ROUND":
                    return Expression.Call(left.Type == typeof(double) ? MethodProvider.DoubleRoundMethod : MethodProvider.DecimalRoundMethod, left);
                case "FLOOR":
                    return Expression.Call(left.Type == typeof(double) ? MethodProvider.DoubleFloorMethod : MethodProvider.DecimalFloorMethod, left);
                case "CEILING":
                    return Expression.Call(left.Type == typeof(double) ? MethodProvider.DoubleCeilingMethod : MethodProvider.DecimalCeilingMethod, left);
                case "ANY":
                case "ALL":
                    {
                        return CreateAnyAllExpression(
                                                      left,
                                                      right,
                                                      sourceParameter,
                                                      lambdaParameters,
                                                      MethodProvider.GetAnyAllMethod(function.Capitalize(), left.Type));
                    }
                default:
                    return null;
            }
        }

        /// <summary>
        /// Reads a date part, such as <c>Year</c>, from a <see cref="DateTime"/> or <see cref="DateTimeOffset"/> value.
        /// A nullable value yields a nullable part that is <c>null</c> when the value is.
        /// </summary>
        private static Expression GetDatePart(Expression instance, string function)
        {
            Type? underlyingType = Nullable.GetUnderlyingType(instance.Type);
            PropertyInfo property = MethodProvider.GetDatePartProperty(underlyingType ?? instance.Type, function)
                ?? throw new InvalidOperationException(function + "() requires a DateTime or DateTimeOffset value, not " + instance.Type.Name + ".");

            if (underlyingType is null)
            {
                return Expression.Property(instance, property);
            }

            Type nullablePartType = typeof(Nullable<>).MakeGenericType(property.PropertyType);
            return Expression.Condition(
                Expression.Property(instance, nameof(Nullable<int>.HasValue)),
                Expression.Convert(Expression.Property(Expression.Property(instance, nameof(Nullable<int>.Value)), property), nullablePartType),
                Expression.Constant(null, nullablePartType));
        }

        /// <summary>
        /// Converts the non-nullable side of a binary operation to the nullable type of the other side, e.g. <c>int</c> to <c>int?</c>,
        /// because expression trees require both operands of a comparison or arithmetic operation to have the same type.
        /// </summary>
        private static void LiftToNullable(ref Expression left, ref Expression right)
        {
            if (Nullable.GetUnderlyingType(left.Type) == right.Type)
            {
                right = Expression.Convert(right, left.Type);
            }
            else if (Nullable.GetUnderlyingType(right.Type) == left.Type)
            {
                left = Expression.Convert(left, right.Type);
            }
        }

        private static Expression CreateAnyAllExpression(
            Expression left,
            Expression? right,
            ParameterExpression sourceParameter,
            IEnumerable<ParameterExpression> lambdaParameters,
            MethodInfo anyAllMethod)
        {
            Type genericFunc = typeof(Func<,>)
                .MakeGenericType(
                                 MethodProvider.GetIEnumerableImpl(left.Type).GetGenericArguments()[0],
                                 typeof(bool));

            var filteredParameters = new ParameterVisitor()
                .GetParameters(right)
                .Where(p => p.Name != sourceParameter.Name)
                .ToArray();
            if (!filteredParameters.Any())
            {
                filteredParameters = lambdaParameters.ToArray();
            }

            return Expression.Call(
                                   anyAllMethod,
                                   left,
                                   Expression.Lambda(genericFunc, right, filteredParameters));
        }

        /// <summary>
        /// Reads a quoted string literal. An unescaped quote inside the text, such as the apostrophe in
        /// <c>'O'Brien'</c>, is kept as-is; a doubled <c>''</c> is read as a single quote. The literal must be the whole
        /// token, so trailing text such as <c>'abc'def</c> is rejected.
        /// </summary>
        /// <returns><c>true</c> when <paramref name="token"/> is a string literal; <c>false</c> when it does not start with a quote.</returns>
        /// <exception cref="FormatException">The token starts with a quote but is not a whole literal.</exception>
        private static bool TryReadStringLiteral(string token, out string? value)
        {
            value = null;
            if (token.Length == 0 || (token[0] != '\'' && token[0] != '"'))
            {
                return false;
            }

            if (token.GetStringLiteralEnd() != token.Length - 1)
            {
                throw new FormatException("Unterminated string literal: " + token);
            }

            value = token.Substring(1, token.Length - 2).Replace("''", "'");
            return true;
        }

        private static Type GetNonNullableType(Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)
                    ? type.GetGenericArguments()[0]
                    : type;
        }

        private static bool SupportsNegate(Type type)
        {
            type = GetNonNullableType(type);
            if (!type.IsEnum)
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Int16:
                    case TypeCode.Int32:
                    case TypeCode.Int64:
                    case TypeCode.Double:
                    case TypeCode.Single:
                        return true;
                }
            }

            return false;
        }

        private Expression? GetBooleanExpression(string filter, IFormatProvider formatProvider)
        {
            if (_valueReader.Read(typeof(bool), filter, formatProvider) is ConstantExpression booleanExpression && booleanExpression.Value is not null)
            {
                return booleanExpression;
            }

            return null;
        }

        private Expression? GetParameterExpression(string filter, Type? type, IFormatProvider formatProvider)
        {
            if (type is null)
            {
                return GetBooleanExpression(filter, formatProvider);
            }

            if (GetNonNullableType(type) == typeof(bool) && !_valueReader.HasCustomFactory(type) && !_valueReader.HasCustomFactory(typeof(bool)))
            {
                if (!BooleanExpressionFactory.TryParse(filter, out bool boolean))
                {
                    throw new InvalidOperationException("Could not read " + filter + " as boolean.");
                }

                return type == typeof(bool) ? Expression.Constant(boolean) : Expression.Convert(Expression.Constant(boolean), type);
            }

            return _valueReader.Read(type, filter, formatProvider);
        }

        private Type? GetExpressionType<T>(TokenSet? set, ParameterExpression parameter, ICollection<ParameterExpression> lambdaParameters, int depth)
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();

            // This lookahead recurses once per arithmetic level and rescans the remaining text each time, so without
            // the same depth limit the main parse uses, deeply nested arithmetic would cost quadratic time here before
            // the parse ever rejects it.
            if (depth > _maxDepth)
            {
                throw DepthExceeded();
            }

            if (set is null)
            {
                return null;
            }

            if (set.Left.IsFunction())
            {
                var functionName = set.Left.GetFunctionName();
                if (!string.IsNullOrWhiteSpace(functionName))
                {
                    return functionName.GetFunctionType();
                }
            }

            var property = GetPropertyExpression<T>(set.Left, parameter, lambdaParameters, depth + 1) ?? GetPropertyExpression<T>(set.Right, parameter, lambdaParameters, depth + 1);
            if (property != null)
            {
                return property.Type;
            }

            var type = GetExpressionType<T>(set.Left.GetArithmeticToken(), parameter, lambdaParameters, depth + 1);

            return type ?? GetExpressionType<T>(set.Right.GetArithmeticToken(), parameter, lambdaParameters, depth + 1);
        }

        private Expression? GetPropertyExpression<T>(string propertyToken, ParameterExpression parameter, ICollection<ParameterExpression> lambdaParameters, int depth)
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();

            if (depth > _maxDepth)
            {
                throw DepthExceeded();
            }

            if (string.IsNullOrWhiteSpace(propertyToken))
            {
                return null;
            }

            propertyToken = propertyToken.StripEnclosingParentheses();

            var token = propertyToken.GetTokens().FirstOrDefault();
            if (token != null)
            {
                return GetPropertyExpression<T>(token.Left, parameter, lambdaParameters, depth + 1) ?? GetPropertyExpression<T>(token.Right, parameter, lambdaParameters, depth + 1);
            }

            return GetMemberExpression<T>(propertyToken, parameter, lambdaParameters);
        }

        /// <summary>
        /// Resolves <paramref name="propertyToken"/> as a member path, such as <c>Child/Age</c>, from the source or a
        /// lambda parameter.
        /// </summary>
        private Expression? GetMemberExpression<T>(string propertyToken, ParameterExpression parameter, ICollection<ParameterExpression> lambdaParameters)
        {
            Type parentType = parameter.Type;
            Expression? propertyExpression = null;

            string[] propertyChain = propertyToken.Split('/');
            if (propertyChain.Length > 0 && lambdaParameters.Any(p => p.Name == propertyChain[0]))
            {
                ParameterExpression lambdaParameter = lambdaParameters.First(p => p.Name == propertyChain[0]);

                parentType = lambdaParameter.Type;
                propertyExpression = lambdaParameter;
            }

            propertyExpression = _memberNameResolver.CreateMemberExpression(parameter, propertyChain, parentType, propertyExpression).Item2;

            return propertyExpression;
        }

        private InvalidOperationException DepthExceeded()
        {
            return new InvalidOperationException("The filter is nested more than " + _maxDepth.ToString(CultureInfo.InvariantCulture) + " levels deep.");
        }

        private Expression? CreateExpression<T>(string filter, ParameterExpression sourceParameter, ICollection<ParameterExpression> lambdaParameters, Type? type, IFormatProvider formatProvider, bool ignoreCase, int depth)
        {
            // The parser recurses once per nesting level of the filter, so a filter nested past MaxDepth is
            // rejected before its remaining levels are parsed. A filter nested deeply enough to exhaust the stack
            // first must still fail as a catchable InsufficientExecutionStackException, because a stack overflow
            // cannot be caught and ends the process.
            if (depth > _maxDepth)
            {
                throw DepthExceeded();
            }

            RuntimeHelpers.EnsureSufficientExecutionStack();

            if (string.IsNullOrWhiteSpace(filter))
            {
                return null;
            }

            // A condition, member or literal in parentheses, such as (IsActive) or (true), is read without them.
            filter = filter.StripEnclosingParentheses(out int levels);
            depth += levels;
            if (depth > _maxDepth)
            {
                throw DepthExceeded();
            }

            if (string.IsNullOrWhiteSpace(filter))
            {
                return null;
            }

            ICollection<TokenSet> tokens = filter.GetTokens();

            if (tokens.Any())
            {
                return GetTokenExpression<T>(sourceParameter, lambdaParameters, formatProvider, tokens, ignoreCase, depth);
            }

            if (string.Equals(filter, "null", StringComparison.OrdinalIgnoreCase))
            {
                return Expression.Constant(null);
            }

            if (filter[0] == '\'' || filter[0] == '"')
            {
                bool isWholeStringLiteral = filter.IsWholeStringLiteral();

                // An arithmetic expression can start with a literal operand, such as 'a' add 1.
                Expression? literalArithmetic = isWholeStringLiteral
                    ? null
                    : GetArithmeticExpression<T>(filter, sourceParameter, lambdaParameters, type, formatProvider, ignoreCase, depth);
                if (literalArithmetic is not null)
                {
                    return literalArithmetic;
                }

                // A quoted literal compared with an enum member is read as that enum (by member name), not as text.
                // The value reader still lets a custom factory for the type win, and reads the member against the
                // known enum type so no enum type is resolved from the literal's own text.
                if (type is not null && isWholeStringLiteral && GetNonNullableType(type).IsEnum)
                {
                    return _valueReader.Read(type, filter, formatProvider);
                }

                TryReadStringLiteral(filter, out string? stringLiteral);
                return Expression.Constant(stringLiteral, typeof(string));
            }

            if (NegateRx.IsMatch(filter))
            {
                Expression? negateExpression = CreateExpression<T>(
                    filter.Substring(1),
                    sourceParameter,
                    lambdaParameters,
                    type,
                    formatProvider,
                    ignoreCase,
                    depth + 1);

                if (negateExpression is not null && SupportsNegate(negateExpression.Type))
                {
                    return Expression.Negate(negateExpression);
                }

                throw new InvalidOperationException("Cannot negate " + negateExpression);
            }

            Expression? expression = GetAnyAllFunctionExpression<T>(filter, sourceParameter, lambdaParameters, formatProvider, ignoreCase, depth)
                ?? GetMemberExpression<T>(filter, sourceParameter, lambdaParameters)
                ?? GetArithmeticExpression<T>(filter, sourceParameter, lambdaParameters, type, formatProvider, ignoreCase, depth)
                ?? GetFunctionExpression<T>(filter, sourceParameter, lambdaParameters, type, formatProvider, ignoreCase, depth)
                ?? GetParameterExpression(filter, type, formatProvider)
                ?? GetBooleanExpression(filter, formatProvider);

            return expression ?? throw new InvalidOperationException("Could not create expression from: " + filter);
        }

        /// <summary>
        /// Builds the conditions in <paramref name="tokens"/> and combines them by precedence: each run of
        /// and-combined conditions first, then those runs with or. A comparison reads its literals by the type of its
        /// own members; a type from the enclosing expression does not apply to a condition.
        /// </summary>
        private Expression? GetTokenExpression<T>(ParameterExpression parameter, ICollection<ParameterExpression> lambdaParameters, IFormatProvider formatProvider, ICollection<TokenSet> tokens, bool ignoreCase, int depth)
        {
            string? combiner = null;
            Expression? orCombined = null;
            Expression? andCombined = null;

            // Operands and and/or combiners must alternate, starting and ending with an operand, and every operand
            // must parse. Anything else makes the filter invalid rather than silently dropping a condition.
            bool awaitingOperand = true;
            foreach (TokenSet tokenSet in tokens)
            {
                bool isCombiner = string.IsNullOrWhiteSpace(tokenSet.Left) && !tokenSet.Operation.IsUnaryOperation();
                if (isCombiner)
                {
                    if (awaitingOperand || !string.IsNullOrWhiteSpace(tokenSet.Right))
                    {
                        return null;
                    }

                    combiner = tokenSet.Operation;
                    awaitingOperand = true;
                    continue;
                }

                if (!awaitingOperand)
                {
                    return null;
                }

                Expression? operand = string.IsNullOrWhiteSpace(tokenSet.Left)
                    ? GetUnaryOperand<T>(tokenSet, parameter, lambdaParameters, formatProvider, ignoreCase, depth)
                    : GetBinaryOperand<T>(tokenSet, parameter, lambdaParameters, formatProvider, ignoreCase, depth);
                if (operand is null)
                {
                    return null;
                }

                if (combiner is null)
                {
                    andCombined = operand;
                }
                else if (combiner.IsOrOperation())
                {
                    orCombined = orCombined is null ? andCombined : GetOperation(combiner, orCombined, andCombined!, ignoreCase);
                    andCombined = operand;
                }
                else
                {
                    andCombined = GetOperation(combiner, andCombined, operand, ignoreCase);
                }

                awaitingOperand = false;
            }

            if (awaitingOperand)
            {
                return null;
            }

            return orCombined is null ? andCombined : GetOperation("or", orCombined, andCombined!, ignoreCase);
        }

        private Expression? GetUnaryOperand<T>(TokenSet tokenSet, ParameterExpression parameter, ICollection<ParameterExpression> lambdaParameters, IFormatProvider formatProvider, bool ignoreCase, int depth)
        {
            // The operand of not is a condition, so no member inside it gives a type to read literals as.
            Expression? right = CreateExpression<T>(
                                            tokenSet.Right,
                                            parameter,
                                            lambdaParameters,
                                            null,
                                            formatProvider,
                                            ignoreCase,
                                            depth + 1);

            return right is null ? null : GetOperation(tokenSet.Operation, null, right, ignoreCase);
        }

        /// <summary>
        /// Builds a token with a left side: a comparison or combination of left and right, or, for a whole condition,
        /// the left side alone. A token with an operation whose right side does not parse is invalid.
        /// </summary>
        private Expression? GetBinaryOperand<T>(TokenSet tokenSet, ParameterExpression parameter, ICollection<ParameterExpression> lambdaParameters, IFormatProvider formatProvider, bool ignoreCase, int depth)
        {
            // A whole condition and the operands of and/or, like the operand of not, get no type for their literals.
            bool isCondition = tokenSet.IsWholeCondition;
            bool combinesConditions = tokenSet.Operation.IsBinaryCombinationOperation();
            Expression? left = CreateExpression<T>(
                                           tokenSet.Left,
                                           parameter,
                                           lambdaParameters,
                                           isCondition || combinesConditions ? null : GetExpressionType<T>(tokenSet, parameter, lambdaParameters, depth),
                                           formatProvider,
                                           ignoreCase,
                                           depth + 1);
            if (left is null || isCondition)
            {
                return left;
            }

            Expression? right = IsInOperation(tokenSet.Operation)
                ? GetInListOperand<T>(tokenSet.Right, left.Type, parameter, lambdaParameters, formatProvider, depth)
                : CreateExpression<T>(tokenSet.Right, parameter, lambdaParameters, combinesConditions ? null : left.Type, formatProvider, ignoreCase, depth + 1);

            return right is null ? null : GetOperation(tokenSet.Operation, left, right, ignoreCase);
        }

        private Expression? GetArithmeticExpression<T>(string filter, ParameterExpression parameter, ICollection<ParameterExpression> lambdaParameters, Type? type, IFormatProvider formatProvider, bool ignoreCase, int depth)
        {
            var arithmeticToken = filter.GetArithmeticToken();
            if (arithmeticToken is null)
            {
                return null;
            }

            Type? type1 = type ?? GetExpressionType<T>(arithmeticToken, parameter, lambdaParameters, depth);
            Expression? leftExpression = CreateExpression<T>(arithmeticToken.Left, parameter, lambdaParameters, type1, formatProvider, ignoreCase, depth + 1);
            Expression? rightExpression = CreateExpression<T>(arithmeticToken.Right, parameter, lambdaParameters, type1, formatProvider, ignoreCase, depth + 1);

            return leftExpression == null || rightExpression == null
                    ? null
                    : GetLeftRightOperation(arithmeticToken.Operation, leftExpression, rightExpression, ignoreCase);
        }

        private Expression? GetAnyAllFunctionExpression<T>(string filter, ParameterExpression sourceParameter, ICollection<ParameterExpression> lambdaParameters, IFormatProvider formatProvider, bool ignoreCase, int depth)
        {
            TokenSet? functionTokens = filter.GetAnyAllFunctionTokens();
            if (functionTokens is null)
            {
                return null;
            }

            Expression? propertyExpression = GetPropertyExpression<T>(functionTokens.Left, sourceParameter, lambdaParameters, depth);
            Type? leftType = propertyExpression?.Type;
            Expression? left = CreateExpression<T>(
                functionTokens.Left,
                sourceParameter,
                lambdaParameters,
                leftType,
                formatProvider,
                ignoreCase,
                depth + 1);

            if (left is null)
            {
                return null;
            }

            var elementType = MethodProvider.GetIEnumerableImpl(left.Type).GetGenericArguments()[0];
            var separatorIndex = functionTokens.Right.IndexOf(':');
            if (separatorIndex < 0)
            {
                // OData allows any() without a lambda to test for a non-empty collection; all() always requires one.
                if (functionTokens.Operation == "any" && string.IsNullOrWhiteSpace(functionTokens.Right))
                {
                    return Expression.Call(typeof(Enumerable), nameof(Enumerable.Any), new[] { elementType }, left);
                }

                throw new InvalidOperationException("Could not create expression from: " + filter + ". Expected a lambda of the form " + functionTokens.Operation + "(x: <expression>).");
            }

            // Create a new ParameterExpression from the lambda parameter and add to a collection to pass around
            var parameterName = functionTokens.Right.Substring(0, separatorIndex).Trim();
            var lambdaParameter = Expression.Parameter(elementType, parameterName);
            lambdaParameters.Add(lambdaParameter);
            var lambdaFilter = functionTokens.Right.Substring(separatorIndex + 1).Trim();

            // The lambda body is a condition, so the collection's type gives no type to read its literals as.
            var isLambdaAnyAllFunction = lambdaFilter.GetAnyAllFunctionTokens() != null;
            var right = isLambdaAnyAllFunction
                ? GetAnyAllFunctionExpression<T>(lambdaFilter, lambdaParameter, lambdaParameters, formatProvider, ignoreCase, depth + 1)
                : CreateExpression<T>(lambdaFilter, sourceParameter, lambdaParameters, null, formatProvider, ignoreCase, depth + 1);

            return GetFunction(functionTokens.Operation, left, right, sourceParameter, lambdaParameters, ignoreCase);
        }

        private Expression? GetFunctionExpression<T>(string filter, ParameterExpression sourceParameter, ICollection<ParameterExpression> lambdaParameters, Type? type, IFormatProvider formatProvider, bool ignoreCase, int depth)
        {
            var functionTokens = filter.GetFunctionTokens();
            if (functionTokens is null)
            {
                return null;
            }

            Expression? left = CreateExpression<T>(
                functionTokens.Left,
                sourceParameter,
                lambdaParameters,
                type ?? GetExpressionType<T>(functionTokens, sourceParameter, lambdaParameters, depth),
                formatProvider,
                ignoreCase,
                depth + 1);

            if (left is null)
            {
                return null;
            }

            Expression? right = CreateExpression<T>(
                                functionTokens.Right,
                                sourceParameter,
                                lambdaParameters,
                                GetFunctionParameterType(functionTokens.Operation) ?? left.Type,
                                formatProvider,
                                ignoreCase,
                                depth + 1);

            return GetFunction(functionTokens.Operation, left, right, sourceParameter, lambdaParameters, ignoreCase);
        }

        /// <summary>
        /// Used to get the ParameterExpressions used in an Expression so that Expression.Call will have the correct number of parameters supplied.
        /// </summary>
        private sealed class ParameterVisitor : ExpressionVisitor
        {
            private static readonly string[] AnyAllMethodNames = { "Any", "All" };
            private List<ParameterExpression> _parameters = new List<ParameterExpression>();

            public IEnumerable<ParameterExpression> GetParameters(Expression? expr)
            {
                _parameters = new List<ParameterExpression>();
                Visit(expr);
                return _parameters;
            }

            public override Expression? Visit(Expression? node)
            {
                // Runs on a partly built tree, before the depth of the whole filter is known.
                RuntimeHelpers.EnsureSufficientExecutionStack();

                if (node is not null && node.NodeType == ExpressionType.Call && AnyAllMethodNames.Contains(((MethodCallExpression)node).Method.Name))
                {
                    // Skip the second parameter of the Any/All as this has already been covered
                    return base.Visit(((MethodCallExpression)node).Arguments.First());
                }

                return base.Visit(node);
            }

            protected override Expression VisitBinary(BinaryExpression node)
            {
                if (node.NodeType == ExpressionType.AndAlso)
                {
                    Visit(node.Left);
                    Visit(node.Right);
                    return node;
                }

                return base.VisitBinary(node);
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (!_parameters.Contains(node))
                {
                    _parameters.Add(node);
                }

                return base.VisitParameter(node);
            }
        }
    }
}