using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

namespace LinqConvertTools.Extensions
{
    /// <summary>
    /// Rebinds the parameters of an expression tree onto a parameter of another type.
    /// </summary>
    /// <remarks>
    /// Member accesses on a rebound parameter are re-resolved by name on the new type, one path segment at a time,
    /// so nested paths keep their shape. Generic static methods (e.g. <see cref="Enumerable.Any{TSource}(IEnumerable{TSource}, Func{TSource, bool})"/>)
    /// are re-closed over the new element types and their lambda parameters are retyped accordingly.
    /// Sub-trees that do not reference a rebound parameter (constants, closures, static members) are left untouched.
    /// </remarks>
    internal sealed class ParameterRebinder : ExpressionVisitor
    {
        private readonly Dictionary<ParameterExpression, Expression> _replacements;
        private readonly string? _memberPath;
        private readonly string? _replacementMemberPath;

        private ParameterRebinder(Dictionary<ParameterExpression, Expression> replacements, string? memberPath, string? replacementMemberPath)
        {
            _replacements = replacements;
            _memberPath = memberPath;
            _replacementMemberPath = replacementMemberPath;
        }

        /// <summary>
        /// Rebinds <paramref name="expression"/> onto a parameter of <paramref name="targetType"/>.
        /// </summary>
        /// <param name="expression">
        /// The expression to rebind. For a lambda, its first parameter is rebound; otherwise every parameter that is not
        /// declared by a lambda inside the expression is rebound.
        /// </param>
        /// <param name="targetType">The type of the new parameter.</param>
        /// <param name="targetParameter">The parameter to bind to, or null to create one named "x".</param>
        /// <param name="memberPath">An optional dot-separated member path, relative to the rebound parameter, to replace.</param>
        /// <param name="replacementMemberPath">The dot-separated member path that replaces <paramref name="memberPath"/>.</param>
        public static Expression Rebind(Expression expression, Type targetType, ParameterExpression? targetParameter, string? memberPath = null, string? replacementMemberPath = null)
        {
            var replacements = new Dictionary<ParameterExpression, Expression>();

            if (expression is LambdaExpression lambdaExpression)
            {
                if (lambdaExpression.Parameters.Count == 0)
                {
                    return new ParameterRebinder(replacements, memberPath, replacementMemberPath).Visit(lambdaExpression);
                }

                ParameterExpression lambdaTarget = targetParameter ?? Expression.Parameter(targetType, "x");
                replacements[lambdaExpression.Parameters[0]] = lambdaTarget;

                Expression body = new ParameterRebinder(replacements, memberPath, replacementMemberPath).Visit(lambdaExpression.Body);
                IEnumerable<ParameterExpression> parameters = new[] { lambdaTarget }.Concat(lambdaExpression.Parameters.Skip(1));
                return Expression.Lambda(body, lambdaExpression.Name, lambdaExpression.TailCall, parameters);
            }

            ParameterExpression target = targetParameter ?? Expression.Parameter(targetType, "x");
            foreach (ParameterExpression freeParameter in FreeParameterCollector.Collect(expression))
            {
                replacements[freeParameter] = target;
            }

            return new ParameterRebinder(replacements, memberPath, replacementMemberPath).Visit(expression);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return _replacements.TryGetValue(node, out Expression? replacement) ? replacement : node;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            Expression body = Visit(node.Body);
            if (body == node.Body)
            {
                return node;
            }

            return body.Type == node.Body.Type
                ? node.Update(body, node.Parameters)
                : Expression.Lambda(body, node.Name, node.TailCall, node.Parameters);
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (_memberPath is not null
                && _replacementMemberPath is not null
                && TryGetRootedMemberPath(node, out ParameterExpression? root, out string? path)
                && string.Equals(path, _memberPath, StringComparison.Ordinal))
            {
                return BindMemberPath(Visit(root!), _replacementMemberPath);
            }

            Expression? instance = Visit(node.Expression);
            if (instance == node.Expression || instance is null)
            {
                return node;
            }

            return BindMember(instance, node.Member);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            Expression operand = Visit(node.Operand);
            if (operand == node.Operand)
            {
                return node;
            }

            switch (node.NodeType)
            {
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    return Reconvert(operand, node);
                case ExpressionType.TypeAs:
                    return Expression.TypeAs(operand, node.Type);
                default:
                    return node.Update(operand);
            }
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            Expression left = Visit(node.Left);
            Expression right = Visit(node.Right);
            if (left == node.Left && right == node.Right)
            {
                return node;
            }

            if (left.NodeType == ExpressionType.MemberAccess && right is UnaryExpression rightUnaryExpression && rightUnaryExpression.NodeType == ExpressionType.Convert)
            {
                right = Expression.Convert(rightUnaryExpression.Operand, left.Type);
            }
            else if (right.NodeType == ExpressionType.MemberAccess && left is UnaryExpression leftUnaryExpression && leftUnaryExpression.NodeType == ExpressionType.Convert)
            {
                left = Expression.Convert(leftUnaryExpression.Operand, right.Type);
            }

            try
            {
                return node.Update(left, node.Conversion, right);
            }
            catch (InvalidOperationException) when (left.Type != right.Type)
            {
                if (GetImplicitOperator(right.Type, left.Type) is MethodInfo opRToL)
                {
                    right = ConvertUsing(right, left.Type, opRToL);
                }
                else if (GetImplicitOperator(left.Type, right.Type) is MethodInfo opLToR)
                {
                    left = ConvertUsing(left, right.Type, opLToR);
                }
                else if (Nullable.GetUnderlyingType(left.Type) == right.Type)
                {
                    right = Expression.Convert(right, left.Type);
                }
                else if (Nullable.GetUnderlyingType(right.Type) == left.Type)
                {
                    left = Expression.Convert(left, right.Type);
                }
                else
                {
                    throw;
                }

                return node.Update(left, node.Conversion, right);
            }
        }

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            Expression test = Visit(node.Test);
            Expression ifTrue = Visit(node.IfTrue);
            Expression ifFalse = Visit(node.IfFalse);
            if (test == node.Test && ifTrue == node.IfTrue && ifFalse == node.IfFalse)
            {
                return node;
            }

            return ifTrue.Type == ifFalse.Type
                ? Expression.Condition(test, ifTrue, ifFalse)
                : Expression.Condition(test, CoerceOrThrow(ifTrue, node.Type), CoerceOrThrow(ifFalse, node.Type), node.Type);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.IsGenericMethod && node.Object is null)
            {
                return VisitGenericStaticMethodCall(node);
            }

            Expression? instance = Visit(node.Object);
            ReadOnlyCollection<Expression> arguments = Visit(node.Arguments);
            if (instance == node.Object && arguments == node.Arguments)
            {
                return node;
            }

            MethodInfo method = node.Method;
            if (instance is not null && method.DeclaringType is Type declaringType && !declaringType.IsAssignableFrom(instance.Type))
            {
                Type[] argumentTypes = arguments.Select(a => a.Type).ToArray();
                if (FindInstanceMethod(instance.Type, method.Name, argumentTypes) is MethodInfo rebound)
                {
                    method = rebound;
                }
                else
                {
                    instance = CoerceOrThrow(instance, declaringType);
                }
            }

            return Expression.Call(instance, method, CoerceArguments(arguments, method.GetParameters()));
        }

        private Expression VisitGenericStaticMethodCall(MethodCallExpression node)
        {
            MethodInfo definition = node.Method.GetGenericMethodDefinition();
            ParameterInfo[] formalParameters = definition.GetParameters();
            var bindings = new Dictionary<Type, Type>();
            var arguments = new Expression[node.Arguments.Count];
            bool changed = false;

            for (int i = 0; i < arguments.Length; i++)
            {
                Expression original = node.Arguments[i];
                Type formalType = formalParameters[i].ParameterType;

                arguments[i] = TryGetLambdaArgument(original, out LambdaExpression? lambda, out bool quoted)
                    ? VisitLambdaArgument(lambda!, quoted, Substitute(formalType, bindings))
                    : Visit(original);

                Unify(formalType, arguments[i].Type, bindings);
                changed |= arguments[i] != original;
            }

            if (!changed)
            {
                return node;
            }

            Type[] typeArguments = node.Method.GetGenericArguments();
            Type[] reboundTypeArguments = definition.GetGenericArguments()
                .Select((typeParameter, index) => bindings.TryGetValue(typeParameter, out Type? bound) ? bound : typeArguments[index])
                .ToArray();

            MethodInfo method = definition.MakeGenericMethod(reboundTypeArguments);
            return Expression.Call(method, CoerceArguments(arguments, method.GetParameters()));
        }

        private Expression VisitLambdaArgument(LambdaExpression lambda, bool quoted, Type formalType)
        {
            Type delegateType = formalType.IsGenericType && formalType.GetGenericTypeDefinition() == typeof(Expression<>)
                ? formalType.GetGenericArguments()[0]
                : formalType;
            MethodInfo? invoke = typeof(Delegate).IsAssignableFrom(delegateType) ? delegateType.GetMethod("Invoke") : null;
            ParameterInfo[] invokeParameters = invoke?.GetParameters() ?? Array.Empty<ParameterInfo>();

            var parameters = new ParameterExpression[lambda.Parameters.Count];
            for (int i = 0; i < parameters.Length; i++)
            {
                ParameterExpression original = lambda.Parameters[i];
                Type? reboundType = i < invokeParameters.Length ? invokeParameters[i].ParameterType : null;
                if (reboundType is null || reboundType.ContainsGenericParameters || reboundType == original.Type)
                {
                    parameters[i] = original;
                }
                else
                {
                    parameters[i] = Expression.Parameter(reboundType, original.Name);
                    _replacements[original] = parameters[i];
                }
            }

            Expression body;
            try
            {
                body = Visit(lambda.Body);
            }
            finally
            {
                foreach (ParameterExpression original in lambda.Parameters)
                {
                    _replacements.Remove(original);
                }
            }

            if (body == lambda.Body && parameters.SequenceEqual(lambda.Parameters))
            {
                return quoted ? Expression.Quote(lambda) : lambda;
            }

            LambdaExpression rebound = !delegateType.ContainsGenericParameters && invoke?.ReturnType == body.Type
                ? Expression.Lambda(delegateType, body, lambda.Name, lambda.TailCall, parameters)
                : Expression.Lambda(body, lambda.Name, lambda.TailCall, parameters);

            return quoted ? Expression.Quote(rebound) : rebound;
        }

        private static bool TryGetLambdaArgument(Expression argument, out LambdaExpression? lambda, out bool quoted)
        {
            quoted = argument.NodeType == ExpressionType.Quote;
            lambda = (quoted ? ((UnaryExpression)argument).Operand : argument) as LambdaExpression;
            return lambda is not null;
        }

        private bool TryGetRootedMemberPath(MemberExpression node, out ParameterExpression? root, out string? path)
        {
            var segments = new Stack<string>();
            Expression? current = node;
            while (current is MemberExpression member)
            {
                segments.Push(member.Member.Name);
                current = member.Expression;
            }

            root = current as ParameterExpression;
            path = string.Join(".", segments);
            return root is not null && _replacements.ContainsKey(root);
        }

        private static Expression BindMemberPath(Expression instance, string memberPath)
        {
            Expression current = instance;
            foreach (string memberName in memberPath.Split('.'))
            {
                current = BindMemberByName(current, memberName)
                    ?? throw new ArgumentException($"'{memberName}' is not a member of type '{current.Type}'.", nameof(memberPath));
            }

            return current;
        }

        private static Expression BindMember(Expression instance, MemberInfo member)
        {
            Type? declaringType = member.DeclaringType;
            if (declaringType is not null && !declaringType.IsInterface && declaringType.IsAssignableFrom(instance.Type))
            {
                return Expression.MakeMemberAccess(instance, member);
            }

            if (declaringType is not null
                && declaringType.IsGenericType
                && declaringType.GetGenericTypeDefinition() == typeof(Nullable<>)
                && Nullable.GetUnderlyingType(declaringType) == instance.Type)
            {
                switch (member.Name)
                {
                    case nameof(Nullable<int>.Value):
                        return instance;
                    case nameof(Nullable<int>.HasValue):
                        return Expression.Constant(true);
                }
            }

            if (BindMemberByName(instance, member.Name) is Expression boundByName)
            {
                return boundByName;
            }

            if (declaringType is not null && declaringType.IsAssignableFrom(instance.Type))
            {
                return Expression.MakeMemberAccess(instance, member);
            }

            if (declaringType is not null && Coerce(instance, declaringType) is Expression coerced)
            {
                return Expression.MakeMemberAccess(coerced, member);
            }

            throw new ArgumentException($"'{member.Name}' is not a member of type '{instance.Type}'.", nameof(member));
        }

        private static Expression? BindMemberByName(Expression instance, string memberName)
        {
            MemberInfo? member = FindPropertyOrField(instance.Type, memberName);
            return member is null ? null : Expression.MakeMemberAccess(instance, member);
        }

        /// <summary>
        /// Finds a property or field by name, mirroring <see cref="Expression.PropertyOrField(Expression, string)"/>:
        /// properties before fields, public before non-public, exact case before case-insensitive.
        /// Unlike that method, members inherited by an interface from its base interfaces are also found.
        /// </summary>
        private static MemberInfo? FindPropertyOrField(Type type, string memberName)
        {
            Type[] lookupTypes = type.IsInterface ? new[] { type }.Concat(type.GetInterfaces()).ToArray() : new[] { type };

            foreach (MemberTypes memberType in new[] { MemberTypes.Property, MemberTypes.Field })
            {
                foreach (BindingFlags visibility in new[] { BindingFlags.Public, BindingFlags.NonPublic })
                {
                    MemberInfo? member = lookupTypes
                        .SelectMany(t => t.GetMember(memberName, memberType, visibility | BindingFlags.Instance | BindingFlags.IgnoreCase))
                        .Where(m => m is not PropertyInfo property || property.GetIndexParameters().Length == 0)
                        .OrderBy(m => string.Equals(m.Name, memberName, StringComparison.Ordinal) ? 0 : 1)
                        .ThenByDescending(m => GetInheritanceDepth(m.DeclaringType))
                        .FirstOrDefault();

                    if (member is not null)
                    {
                        return member;
                    }
                }
            }

            return null;
        }

        private static int GetInheritanceDepth(Type? type)
        {
            int depth = 0;
            for (Type? current = type?.BaseType; current is not null; current = current.BaseType)
            {
                depth++;
            }

            return depth;
        }

        private static MethodInfo? FindInstanceMethod(Type type, string name, Type[] argumentTypes)
        {
            IEnumerable<Type> lookupTypes = type.IsInterface ? new[] { type }.Concat(type.GetInterfaces()) : new[] { type };
            return lookupTypes
                .Select(t => t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, argumentTypes, null))
                .FirstOrDefault(m => m is not null);
        }

        private static Expression Reconvert(Expression operand, UnaryExpression node)
        {
            if (operand.Type == node.Type)
            {
                return operand;
            }

            if (node.Method is MethodInfo method && method.GetParameters()[0].ParameterType.IsAssignableFrom(operand.Type))
            {
                return Expression.MakeUnary(node.NodeType, operand, node.Type, method);
            }

            try
            {
                return Expression.MakeUnary(node.NodeType, operand, node.Type);
            }
            catch (InvalidOperationException)
            {
                if (Coerce(operand, node.Type) is Expression coerced)
                {
                    return coerced;
                }

                throw;
            }
        }

        private static Expression[] CoerceArguments(IReadOnlyList<Expression> arguments, ParameterInfo[] parameters)
        {
            return arguments
                .Select((argument, index) => CoerceOrThrow(argument, parameters[index].ParameterType))
                .ToArray();
        }

        private static Expression CoerceOrThrow(Expression expression, Type type)
        {
            return Coerce(expression, type)
                ?? throw new InvalidOperationException($"No implicit conversion exists from '{expression.Type}' to '{type}'.");
        }

        private static Expression? Coerce(Expression expression, Type type)
        {
            if (expression.Type == type)
            {
                return expression;
            }

            if (type.IsAssignableFrom(expression.Type))
            {
                return expression.Type.IsValueType ? Expression.Convert(expression, type) : expression;
            }

            if (GetImplicitOperator(expression.Type, type) is MethodInfo implicitOperator)
            {
                return ConvertUsing(expression, type, implicitOperator);
            }

            return null;
        }

        private static Expression ConvertUsing(Expression expression, Type type, MethodInfo implicitOperator)
        {
            Type parameterType = implicitOperator.GetParameters()[0].ParameterType;
            return Expression.Convert(LiftIfNeeded(expression, parameterType), type, implicitOperator);
        }

        private static Expression LiftIfNeeded(Expression expr, Type parameterType)
        {
            // If the operator expects Nullable<T> and we have T, lift T -> Nullable<T>
            var underlying = Nullable.GetUnderlyingType(parameterType);
            if (underlying is not null && expr.Type == underlying)
            {
                return Expression.Convert(expr, parameterType); // built-in T -> Nullable<T>
            }

            return expr;
        }

        private static MethodInfo? GetImplicitOperator(Type fromType, Type toType)
        {
            static bool Match(ParameterInfo p, Type from, Type to, MethodInfo m)
            {
                // exact: TFrom -> TTo
                if (p.ParameterType == from && m.ReturnType == to)
                {
                    return true;
                }

                // lifted: TFrom? -> TTo?
                var underlyingParameterType = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;
                var underlyingReturnType = Nullable.GetUnderlyingType(m.ReturnType) ?? m.ReturnType;
                return underlyingParameterType == from && underlyingReturnType == to;
            }

            var underlyingFromType = Nullable.GetUnderlyingType(fromType) ?? fromType;
            var underlyingToType = Nullable.GetUnderlyingType(toType) ?? toType;
            foreach (var t in new[] { underlyingFromType, underlyingToType })
            {
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name != "op_Implicit")
                    {
                        continue;
                    }

                    var ps = m.GetParameters();
                    if (ps.Length != 1)
                    {
                        continue;
                    }

                    if (Match(ps[0], fromType, toType, m))
                    {
                        return m;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Infers generic type parameter bindings by matching a formal (open) type against an actual type.
        /// The first binding found for a type parameter wins.
        /// </summary>
        private static void Unify(Type formal, Type actual, Dictionary<Type, Type> bindings)
        {
            if (formal.IsGenericParameter)
            {
                if (!bindings.ContainsKey(formal))
                {
                    bindings[formal] = actual;
                }

                return;
            }

            if (!formal.ContainsGenericParameters)
            {
                return;
            }

            if (formal.IsArray)
            {
                if (actual.IsArray)
                {
                    Unify(formal.GetElementType()!, actual.GetElementType()!, bindings);
                }

                return;
            }

            if (formal.IsGenericType && FindGenericType(actual, formal.GetGenericTypeDefinition()) is Type match)
            {
                Type[] formalArguments = formal.GetGenericArguments();
                Type[] actualArguments = match.GetGenericArguments();
                for (int i = 0; i < formalArguments.Length; i++)
                {
                    Unify(formalArguments[i], actualArguments[i], bindings);
                }
            }
        }

        private static Type Substitute(Type type, Dictionary<Type, Type> bindings)
        {
            if (type.IsGenericParameter)
            {
                return bindings.TryGetValue(type, out Type? bound) ? bound : type;
            }

            if (!type.ContainsGenericParameters)
            {
                return type;
            }

            if (type.IsArray)
            {
                return Substitute(type.GetElementType()!, bindings).MakeArrayType();
            }

            if (type.IsGenericType)
            {
                Type[] arguments = type.GetGenericArguments().Select(a => Substitute(a, bindings)).ToArray();
                return type.GetGenericTypeDefinition().MakeGenericType(arguments);
            }

            return type;
        }

        private static Type? FindGenericType(Type type, Type genericTypeDefinition)
        {
            for (Type? current = type; current is not null; current = current.BaseType)
            {
                if (current.IsGenericType && current.GetGenericTypeDefinition() == genericTypeDefinition)
                {
                    return current;
                }
            }

            return genericTypeDefinition.IsInterface
                ? type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericTypeDefinition)
                : null;
        }

        /// <summary>
        /// Collects the parameters referenced by an expression that are not declared by a lambda inside it.
        /// </summary>
        private sealed class FreeParameterCollector : ExpressionVisitor
        {
            private readonly HashSet<ParameterExpression> _declared = new();
            private readonly List<ParameterExpression> _free = new();

            public static IReadOnlyList<ParameterExpression> Collect(Expression expression)
            {
                var collector = new FreeParameterCollector();
                collector.Visit(expression);
                return collector._free;
            }

            protected override Expression VisitLambda<T>(Expression<T> node)
            {
                _declared.UnionWith(node.Parameters);
                return base.VisitLambda(node);
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (!_declared.Contains(node) && !_free.Contains(node))
                {
                    _free.Add(node);
                }

                return node;
            }
        }
    }
}
