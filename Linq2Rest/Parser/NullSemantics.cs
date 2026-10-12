namespace LinqConvertTools.Parser
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Builds conditions with OData's null semantics (OData 4.01 URL conventions, 5.1.1). A canonical function or an
    /// arithmetic operator given null returns null. <c>eq</c> and <c>ne</c> treat null as a value, so <c>null eq null</c>
    /// is true and <c>null ne 'a'</c> is true; <c>gt</c>, <c>ge</c>, <c>lt</c> and <c>le</c> are false when an operand is
    /// null. A condition that is null, such as <c>contains(Name, 'a')</c> when <c>Name</c> is null, is unknown:
    /// <c>not</c> keeps it unknown, <c>and</c> with false is false, <c>or</c> with true is true, and a filter keeps only
    /// the rows for which it is true.
    /// </summary>
    /// <remarks>
    /// A function is built as a plain method call, which would throw on null in memory, and guarded here by the values
    /// it needs: the instance a method or <see cref="string.Length"/> reads from and the arguments it passes on. A
    /// condition that can be unknown is a <see cref="Condition"/> carrying when it is true and when it is false. The
    /// finished filter is reduced by <see cref="Reduce"/> to when it is true, so every expression it returns is two-valued
    /// and translates to plain SQL predicates.
    /// </remarks>
    internal static class NullSemantics
    {
        private static readonly Expression NullConstant = Expression.Constant(null, typeof(object));
        private static readonly Expression TrueConstant = Expression.Constant(true);
        private static readonly Expression FalseConstant = Expression.Constant(false);

        /// <summary>
        /// Guards a boolean function, such as <c>x.Name.Contains(x.Other)</c>, so that it is unknown when a value it
        /// needs is null.
        /// </summary>
        public static Expression Predicate(Expression predicate)
        {
            List<Expression> required = GetRequiredValues(predicate);
            if (required.Count == 0)
            {
                return predicate;
            }

            Expression present = AllPresent(required, nullFirst: false);
            return new Condition(And(present, predicate), And(present, Expression.Not(predicate)), present);
        }

        /// <summary>
        /// Builds an <c>eq</c>, <c>ne</c>, <c>gt</c>, <c>ge</c>, <c>lt</c> or <c>le</c> comparison whose operands include
        /// a function or arithmetic of a value that can be null.
        /// </summary>
        /// <param name="operation">The comparison, in upper case.</param>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        /// <param name="comparison">Builds the comparison of the operands, evaluated only when neither is null.</param>
        /// <returns>The comparison, or <c>null</c> when neither operand needs a value that can be null, so the plain
        /// comparison already has OData's semantics.</returns>
        public static Expression? Compare(string operation, Expression left, Expression right, Func<Expression> comparison)
        {
            List<Expression> leftRequired = GetRequiredValues(left);
            List<Expression> rightRequired = GetRequiredValues(right);
            if (leftRequired.Count == 0 && rightRequired.Count == 0)
            {
                return null;
            }

            if (operation != "EQ" && operation != "NE")
            {
                // A null operand makes an ordering comparison false. A null value read directly, such as an int?
                // member, already compares false in the lifted comparison.
                return And(AllPresent(leftRequired, nullFirst: true), AllPresent(rightRequired, nullFirst: true), comparison());
            }

            Expression leftIsNull = IsNull(left, leftRequired);
            Expression rightIsNull = IsNull(right, rightRequired);
            Expression leftPresent = IsPresent(left, leftRequired);
            Expression rightPresent = IsPresent(right, rightRequired);
            bool equal = operation == "EQ";

            // When one side can never be null, the comparison is evaluated only once the other side is known to be
            // present, by the short-circuit of AndAlso/OrElse.
            if (IsFalse(rightIsNull))
            {
                return equal ? And(leftPresent, comparison()) : Or(leftIsNull, comparison());
            }

            if (IsFalse(leftIsNull))
            {
                return equal ? And(rightPresent, comparison()) : Or(rightIsNull, comparison());
            }

            Expression bothPresent = And(leftPresent, rightPresent);
            return equal
                ? Or(And(leftIsNull, rightIsNull), And(bothPresent, comparison()))
                : Or(Or(And(leftIsNull, rightPresent), And(leftPresent, rightIsNull)), And(bothPresent, comparison()));
        }

        /// <summary>
        /// Builds <c>left in right</c> when the left operand is a function of a value that can be null, or a condition
        /// that can be unknown. A null left operand is a member of a list only when the list holds null.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The list or collection.</param>
        /// <param name="inOperation">Builds the lookup, evaluated only when the left operand is present.</param>
        /// <returns>The lookup, or <c>null</c> when the plain lookup already has OData's semantics.</returns>
        public static Expression? In(Expression left, Expression right, Func<Expression> inOperation)
        {
            if (left is Condition && right is ConstantExpression { Value: Array items })
            {
                return items.Cast<object?>()
                    .Select(item => Equal(left, Expression.Constant(item, typeof(bool?)), equal: true)!)
                    .Aggregate(FalseConstant, Or);
            }

            List<Expression> required = GetRequiredValues(left);
            if (required.Count == 0)
            {
                return null;
            }

            Expression lookup = And(AllPresent(required, nullFirst: true), inOperation());
            return right is ConstantExpression { Value: Array values } && values.Cast<object?>().Contains(null)
                ? Or(AnyMissing(required), lookup)
                : lookup;
        }

        /// <summary>
        /// Compares two conditions with <c>eq</c> or <c>ne</c> when either can be unknown or is the <c>null</c>
        /// literal. Like any <c>eq</c>, the result is never unknown: <c>contains(Name, 'a') eq false</c> is false when
        /// <c>Name</c> is null, and <c>contains(Name, 'a') eq null</c> is true.
        /// </summary>
        /// <returns>The comparison, or <c>null</c> when neither operand is such a condition.</returns>
        public static Expression? Equal(Expression left, Expression right, bool equal)
        {
            if (left is not Condition && right is not Condition)
            {
                return null;
            }

            if (left is not Condition)
            {
                (left, right) = (right, left);
            }

            var (whenTrue, whenFalse, known) = GetCases(left);
            Expression result;
            if (right is ConstantExpression constant)
            {
                result = constant.Value switch
                {
                    true => equal ? Expression.Equal(whenTrue, TrueConstant) : Expression.Not(whenTrue),
                    false => equal ? whenFalse : Expression.Not(whenFalse),
                    _ => equal ? Negate(known) : known,
                };
            }
            else
            {
                var (otherTrue, otherFalse, otherKnown) = GetCases(right);
                Expression same = Or(
                    Or(And(whenTrue, otherTrue), And(whenFalse, otherFalse)),
                    And(Negate(known), Negate(otherKnown)));
                result = equal ? same : Negate(same);
            }

            return result;
        }

        /// <summary>
        /// Negates a condition: <c>not</c> of an unknown condition is unknown.
        /// </summary>
        /// <returns>The negation, or <c>null</c> when <paramref name="operand"/> cannot be unknown.</returns>
        public static Expression? Not(Expression operand)
        {
            return operand is Condition condition ? new Condition(condition.WhenFalse, condition.WhenTrue, condition.Known) : null;
        }

        /// <summary>
        /// Combines two conditions with <c>and</c> or <c>or</c> when either can be unknown.
        /// </summary>
        /// <returns>The combination, or <c>null</c> when neither operand can be unknown.</returns>
        public static Expression? Combine(Expression left, Expression right, bool isAnd)
        {
            if (left is not Condition && right is not Condition)
            {
                return null;
            }

            var (leftTrue, leftFalse, _) = GetCases(left);
            var (rightTrue, rightFalse, _) = GetCases(right);
            return isAnd
                ? new Condition(And(leftTrue, rightTrue), Or(leftFalse, rightFalse))
                : new Condition(Or(leftTrue, rightTrue), And(leftFalse, rightFalse));
        }

        /// <summary>
        /// Replaces each condition that can be unknown with when it is true, so the filter keeps only the rows for
        /// which it is true.
        /// </summary>
        public static Expression Reduce(Expression expression)
        {
            return new ConditionReducer().Visit(expression);
        }

        /// <summary>
        /// Lists the values that must not be null for <paramref name="operand"/> to be evaluated: the instance a method
        /// or <see cref="string.Length"/> is read from and the string arguments of a string method, at any depth.
        /// Arithmetic, conversions and the arguments of a method are searched through, as is a string function whose
        /// result is passed on, since a string function returns a string whenever its own inputs are present.
        /// </summary>
        private static List<Expression> GetRequiredValues(Expression operand)
        {
            var required = new List<Expression>();
            CollectRequiredValues(operand, required);
            return required;
        }

        private static void CollectRequiredValues(Expression operand, List<Expression> required)
        {
            switch (operand)
            {
                case MethodCallExpression { Object: not null } call:
                    Require(call.Object, required);
                    foreach (Expression argument in call.Arguments)
                    {
                        if (argument.Type == typeof(string) && call.Method.DeclaringType == typeof(string))
                        {
                            Require(argument, required);
                        }
                        else
                        {
                            CollectRequiredValues(argument, required);
                        }
                    }

                    break;
                case MemberExpression { Expression: { Type: var instanceType } instance } when instanceType == typeof(string):
                    Require(instance, required);
                    break;
                case BinaryExpression binary when IsArithmetic(binary.NodeType):
                    CollectRequiredValues(binary.Left, required);
                    CollectRequiredValues(binary.Right, required);
                    break;
                case UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.Negate } unary:
                    CollectRequiredValues(unary.Operand, required);
                    break;
            }
        }

        private static void Require(Expression value, List<Expression> required)
        {
            if (value.Type.IsValueType || value is ConstantExpression { Value: not null })
            {
                return;
            }

            if (IsStringFunction(value))
            {
                CollectRequiredValues(value, required);
                return;
            }

            if (!required.Exists(existing => IsSameValue(existing, value)))
            {
                required.Add(value);
            }
        }

        /// <summary>
        /// Determines whether two expressions read the same value: the same member of the same instance, the same
        /// parameter, or equal constants. Each reference to a member in a filter is parsed into its own node.
        /// </summary>
        private static bool IsSameValue(Expression first, Expression second)
        {
            return (first, second) switch
            {
                _ when ReferenceEquals(first, second) => true,
                (MemberExpression a, MemberExpression b) => a.Member == b.Member && IsSameInstance(a.Expression, b.Expression),
                (ConstantExpression a, ConstantExpression b) => a.Type == b.Type && Equals(a.Value, b.Value),
                _ => false,
            };
        }

        /// <summary>
        /// Determines whether two members are read from the same instance; a static member has none.
        /// </summary>
        private static bool IsSameInstance(Expression? first, Expression? second)
        {
            return first is not null && second is not null ? IsSameValue(first, second) : first is null && second is null;
        }

        private static bool IsArithmetic(ExpressionType nodeType)
        {
            return nodeType is ExpressionType.Add or ExpressionType.Subtract or ExpressionType.Multiply or ExpressionType.Divide or ExpressionType.Modulo;
        }

        /// <summary>
        /// Determines whether <paramref name="value"/> is a string function, such as <c>x.Name.Trim()</c>, which is
        /// never null when its inputs are present.
        /// </summary>
        private static bool IsStringFunction(Expression value)
        {
            return value is MethodCallExpression { Object: not null } call && call.Method.DeclaringType == typeof(string) && call.Type == typeof(string);
        }

        /// <summary>
        /// Builds the test for <paramref name="operand"/> being null: a value it needs is null, or it reads a value
        /// that is null itself, such as a member or the <c>null</c> literal.
        /// </summary>
        private static Expression IsNull(Expression operand, List<Expression> required)
        {
            return operand switch
            {
                ConstantExpression { Value: null } => TrueConstant,
                _ when CanBeNullItself(operand) => Or(AnyMissing(required), Expression.Equal(operand, NullFor(operand.Type))),
                _ => AnyMissing(required),
            };
        }

        private static Expression IsPresent(Expression operand, List<Expression> required)
        {
            return operand switch
            {
                ConstantExpression { Value: null } => FalseConstant,
                _ when CanBeNullItself(operand) => And(AllPresent(required, nullFirst: true), Expression.NotEqual(operand, NullFor(operand.Type))),
                _ => AllPresent(required, nullFirst: true),
            };
        }

        /// <summary>
        /// Determines whether <paramref name="operand"/> can be null even when the values it needs are present: a
        /// member or other value of a reference or nullable type, but not a non-null constant or a string function.
        /// </summary>
        private static bool CanBeNullItself(Expression operand)
        {
            bool nullableType = !operand.Type.IsValueType || Nullable.GetUnderlyingType(operand.Type) is not null;
            return nullableType && operand is not ConstantExpression && !IsStringFunction(operand);
        }

        private static Expression NullFor(Type type)
        {
            return type.IsValueType ? Expression.Constant(null, type) : NullConstant;
        }

        /// <summary>
        /// Builds the guard that every value in <paramref name="required"/> is present, written as
        /// <c>null != value</c> when <paramref name="nullFirst"/> is set and as <c>value != null</c> otherwise.
        /// </summary>
        private static Expression AllPresent(List<Expression> required, bool nullFirst)
        {
            return required
                .Select(value => (Expression)(nullFirst ? Expression.NotEqual(NullFor(value.Type), value) : Expression.NotEqual(value, NullFor(value.Type))))
                .Aggregate(TrueConstant, And);
        }

        private static Expression AnyMissing(List<Expression> required)
        {
            return required
                .Select(value => (Expression)Expression.Equal(NullFor(value.Type), value))
                .Aggregate(FalseConstant, Or);
        }

        /// <summary>
        /// Gets when <paramref name="condition"/> is true, when it is false, and when it is known, that is, not null.
        /// </summary>
        private static (Expression WhenTrue, Expression WhenFalse, Expression Known) GetCases(Expression condition)
        {
            return condition switch
            {
                Condition known => (known.WhenTrue, known.WhenFalse, known.Known),
                ConstantExpression { Value: null } => (FalseConstant, FalseConstant, FalseConstant),
                _ when condition.Type == typeof(bool?) => (
                    Expression.Equal(condition, Expression.Constant(true, typeof(bool?))),
                    Expression.Equal(condition, Expression.Constant(false, typeof(bool?))),
                    Expression.NotEqual(condition, Expression.Constant(null, typeof(bool?)))),
                _ => (condition, Expression.Not(condition), TrueConstant),
            };
        }

        private static bool IsTrue(Expression expression) => expression is ConstantExpression { Value: true };

        private static bool IsFalse(Expression expression) => expression is ConstantExpression { Value: false };

        private static Expression And(Expression left, Expression right)
        {
            return IsTrue(left) ? right
                : IsTrue(right) ? left
                : IsFalse(left) || IsFalse(right) ? FalseConstant
                : Expression.AndAlso(left, right);
        }

        private static Expression And(Expression first, Expression second, Expression third)
        {
            return And(And(first, second), third);
        }

        private static Expression Or(Expression left, Expression right)
        {
            return IsFalse(left) ? right
                : IsFalse(right) ? left
                : IsTrue(left) || IsTrue(right) ? TrueConstant
                : Expression.OrElse(left, right);
        }

        private static Expression Negate(Expression operand)
        {
            return IsTrue(operand) ? FalseConstant
                : IsFalse(operand) ? TrueConstant
                : Expression.Not(operand);
        }

        /// <summary>
        /// A condition that can be unknown, such as <c>contains(Name, 'a')</c> when <c>Name</c> is null: true when
        /// <see cref="WhenTrue"/> is, false when <see cref="WhenFalse"/> is, and unknown when neither is.
        /// </summary>
        private sealed class Condition : Expression
        {
            public Condition(Expression whenTrue, Expression whenFalse, Expression? known = null)
            {
                WhenTrue = whenTrue;
                WhenFalse = whenFalse;
                Known = known ?? Or(whenTrue, whenFalse);
            }

            public Expression WhenTrue { get; }

            public Expression WhenFalse { get; }

            /// <summary>
            /// Gets when the condition is known: true or false rather than null.
            /// </summary>
            public Expression Known { get; }

            public override ExpressionType NodeType => ExpressionType.Extension;

            public override Type Type => typeof(bool);

            public override bool CanReduce => true;

            public override Expression Reduce()
            {
                return WhenTrue;
            }

            protected override Expression VisitChildren(ExpressionVisitor visitor)
            {
                Expression whenTrue = visitor.Visit(WhenTrue);
                Expression whenFalse = visitor.Visit(WhenFalse);
                Expression known = visitor.Visit(Known);
                return ReferenceEquals(whenTrue, WhenTrue) && ReferenceEquals(whenFalse, WhenFalse) && ReferenceEquals(known, Known)
                    ? this
                    : new Condition(whenTrue, whenFalse, known);
            }
        }

        private sealed class ConditionReducer : ExpressionVisitor
        {
            public override Expression? Visit(Expression? node)
            {
                // A chain of conditions is built in a loop, so the finished tree can be deeper than the stack allows.
                RuntimeHelpers.EnsureSufficientExecutionStack();

                return base.Visit(node);
            }

            protected override Expression VisitExtension(Expression node)
            {
                return node is Condition condition ? Visit(condition.WhenTrue) : base.VisitExtension(node);
            }
        }
    }
}
