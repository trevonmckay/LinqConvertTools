using LinqConvertTools.Tests.Fakes;
using LinqConvertTools;
using LinqConvertTools.Extensions;
using NUnit.Framework;
using System.Linq.Expressions;

namespace LinqConvertTools.Tests.Extensions
{
    [TestFixture]
    public class ExpressionExtensionsTests
    {
        [Test]
        public void CastMember()
        {
            // Arrange
            const string filter = "hired ge datetimeoffset'2023-10-13T09:52:00'";
            ODataExpressionConverter converter = new();
            Expression<Func<IQueryableUser, bool>> expression = converter.Convert<IQueryableUser>(filter);

            // Act
            Expression<Func<User, bool>> castedExpression = (Expression<Func<User, bool>>)expression.CastParameter<User>(null);

            // Assert
            Assert.NotNull(castedExpression);
        }

        [Test]
        public void ReplacesMemberName()
        {
            const string filter = "startswith(emailAddress, 'user@')";
            ODataExpressionConverter converter = new();

            Expression<Func<IQueryableUser, bool>> converted = converter.Convert<IQueryableUser>(filter);
            Expression<Func<User, bool>> predicate = (Expression<Func<User, bool>>)converted.ReplaceMemberExpression<User>("EmailAddress", "EmailAddress.Value");

            List<User> users = new()
            {
                new User()
                {
                    GivenName = "User",
                    FamilyName = "One",
                    EmailAddress = new EmailAddress("user@xyz.com")
                },
                new User()
                {
                    GivenName = "User",
                    FamilyName = "Two",
                    EmailAddress = new EmailAddress("nonuser@xyz.com")
                }
            };

            IQueryable<User> query = users.AsQueryable();

            query = query.Where(predicate).Cast<User>();

            Assert.AreEqual(1, query.Count());
        }

        [Test]
        public void ReplacesParameter()
        {
            const string filter = "startswith(familyname, 'Tw')";
            ODataExpressionConverter converter = new();

            Expression<Func<IQueryableUser, bool>> converted = converter.Convert<IQueryableUser>(filter);
            Expression<Func<User, bool>> predicate = (Expression<Func<User, bool>>)converted.ReplaceMemberExpression<User>("EmailAddress", "EmailAddress.Value");

            List<User> users = new()
            {
                new User()
                {
                    GivenName = "User",
                    FamilyName = "One",
                    EmailAddress = new EmailAddress("user@xyz.com")
                },
                new User()
                {
                    GivenName = "User",
                    FamilyName = "Two",
                    EmailAddress = new EmailAddress("nonuser@xyz.com")
                }
            };

            IQueryable<User> query = users.AsQueryable();

            query = query.Where(predicate).Cast<User>();

            Assert.AreEqual(1, query.Count());
        }

        [TestCase("not (name eq 'Alpha')", "Beta,Gamma")]
        [TestCase("not lines/any(l: l/sku eq 'A')", "Beta,Gamma")]
        [TestCase("address/city eq 'Paris'", "Beta")]
        [TestCase("lines/any(l: l/sku eq 'C')", "Beta")]
        [TestCase("lines/all(l: l/quantity gt 1)", "Beta,Gamma")]
        [TestCase("tags/any(t: t eq 'red')", "Alpha")]
        [TestCase("tags/all(t: t ne 'red')", "Beta,Gamma")]
        [TestCase("priority ge 2", "Beta,Gamma")]
        [TestCase("name in ('Alpha', 'Gamma')", "Alpha,Gamma")]
        [TestCase("contains(name, 'ph')", "Alpha")]
        public void CastParameterRebindsFilterOntoEntity(string filter, string expected)
        {
            Expression<Func<Order, bool>> predicate = CastOrderFilter(filter);

            Assert.AreEqual(expected, FilterOrderNames(predicate));
        }

        [TestCase("name eq 'ALPHA'", "Alpha")]
        [TestCase("startswith(name, 'BE')", "Beta")]
        [TestCase("name in ('ALPHA', 'beta')", "Alpha,Beta")]
        [TestCase("contains(name, 'ALP')", "Alpha")]
        [TestCase("address/city eq 'paris'", "Beta")]
        public void CastParameterRebindsCaseInsensitiveFilterOntoEntity(string filter, string expected)
        {
            Expression<Func<Order, bool>> predicate = CastOrderFilter(filter, ignoreCase: true);

            Assert.AreEqual(expected, FilterOrderNames(predicate));
        }

        [Test]
        public void CastParameterLeavesCapturedValuesUntouched()
        {
            var criteria = new { Name = "Beta" };
            string city = "Paris";
            Expression<Func<IOrderSchema, bool>> expression = x => x.Name == criteria.Name || (x.Address != null && x.Address.City == city);

            var predicate = (Expression<Func<Order, bool>>)expression.CastParameter<Order>(null);

            AssertDoesNotReferenceOrderSchema(predicate);
            Assert.AreEqual("Beta", FilterOrderNames(predicate));
        }

        [Test]
        public void CastParameterRetypesQuotedQueryableLambdas()
        {
            Expression<Func<IOrderSchema, bool>> expression = x => x.Lines.AsQueryable().Any(l => l.Sku == "C");

            var predicate = (Expression<Func<Order, bool>>)expression.CastParameter<Order>(null);

            AssertDoesNotReferenceOrderSchema(predicate);
            Assert.AreEqual("Beta", FilterOrderNames(predicate));
        }

        [Test]
        public void CastParameterReclosesGenericMethodsWithInferredResultType()
        {
            Expression<Func<IOrderSchema, bool>> expression = x => x.Lines.Select(l => l.Sku).Contains("A");

            var predicate = (Expression<Func<Order, bool>>)expression.CastParameter<Order>(null);

            AssertDoesNotReferenceOrderSchema(predicate);
            Assert.AreEqual("Alpha", FilterOrderNames(predicate));
        }

        [Test]
        public void CastParameterRebindsOuterParameterInsideNestedLambda()
        {
            // CA1862: This expression tree is test input for parameter rebinding, not a string comparison to optimize.
#pragma warning disable CA1862
            Expression<Func<IOrderSchema, bool>> expression = x => x.Tags.Any(t => t == x.Name!.ToLowerInvariant());
#pragma warning restore CA1862

            var predicate = (Expression<Func<Order, bool>>)expression.CastParameter<Order>(null);

            AssertDoesNotReferenceOrderSchema(predicate);
            Assert.AreEqual("Alpha", FilterOrderNames(predicate));
        }

        [Test]
        public void CastParameterRebindsFreeParametersOfNonLambdaExpression()
        {
            Expression<Func<IOrderSchema, bool>> expression = new ODataExpressionConverter().Convert<IOrderSchema>("address/city eq 'Paris'");
            ParameterExpression parameter = Expression.Parameter(typeof(Order), "o");

            Expression body = expression.Body.CastParameter<Order>(parameter);
            var predicate = Expression.Lambda<Func<Order, bool>>(body, parameter);

            AssertDoesNotReferenceOrderSchema(predicate);
            Assert.AreEqual("Beta", FilterOrderNames(predicate));
        }

        [Test]
        public void ReplacesMemberNameInsideMethodArguments()
        {
            const string filter = "substringof('nonuser', emailAddress)";
            ODataExpressionConverter converter = new();

            Expression<Func<IQueryableUser, bool>> converted = converter.Convert<IQueryableUser>(filter);
            var predicate = (Expression<Func<User, bool>>)converted.ReplaceMemberExpression<User>("EmailAddress", "EmailAddress.Value");

            List<User> users = new()
            {
                new User() { FamilyName = "One", EmailAddress = new EmailAddress("user@xyz.com") },
                new User() { FamilyName = "Two", EmailAddress = new EmailAddress("nonuser@xyz.com") }
            };

            Assert.AreEqual(new[] { "Two" }, users.AsQueryable().Where(predicate).Select(u => u.FamilyName).ToArray());
        }

        [Test]
        public void ReplacesNestedMemberPath()
        {
            Expression<Func<IOrderSchema, bool>> converted = new ODataExpressionConverter().Convert<IOrderSchema>("address/city eq 'Paris'");

            var predicate = (Expression<Func<Order, bool>>)converted.ReplaceMemberExpression<Order>("Address.City", "City");

            AssertDoesNotReferenceOrderSchema(predicate);
            Assert.AreEqual("Alpha", FilterOrderNames(predicate));
        }

        private static List<Order> CreateOrders()
        {
            return new()
            {
                new Order
                {
                    Name = "Alpha",
                    City = "Paris",
                    Address = new OrderAddress { City = "London" },
                    Lines = { new OrderLine { Sku = "A", Quantity = 1 } },
                    Tags = { "red", "alpha" },
                    Priority = 1,
                },
                new Order
                {
                    Name = "Beta",
                    City = "London",
                    Address = new OrderAddress { City = "Paris" },
                    Lines = { new OrderLine { Sku = "B", Quantity = 2 }, new OrderLine { Sku = "C", Quantity = 3 } },
                    Tags = { "blue" },
                    Priority = 2,
                },
                new Order
                {
                    Name = "Gamma",
                    City = "Paris",
                    Priority = 3,
                },
            };
        }

        private static Expression<Func<Order, bool>> CastOrderFilter(string filter, bool ignoreCase = false)
        {
            Expression<Func<IOrderSchema, bool>> converted = new ODataExpressionConverter().Convert<IOrderSchema>(filter, ignoreCase);
            var predicate = (Expression<Func<Order, bool>>)converted.CastParameter<Order>(null);

            AssertDoesNotReferenceOrderSchema(predicate);
            return predicate;
        }

        private static string FilterOrderNames(Expression<Func<Order, bool>> predicate)
        {
            return string.Join(",", CreateOrders().AsQueryable().Where(predicate).Select(o => o.Name));
        }

        private static void AssertDoesNotReferenceOrderSchema(Expression expression)
        {
            Type[] schemaTypes = { typeof(IOrderSchema), typeof(IOrderAddressSchema), typeof(IOrderLineSchema) };
            Type[] referenced = TypeReferenceCollector.Collect(expression).Where(t => schemaTypes.Any(s => TypeReferenceCollector.Mentions(t, s))).ToArray();

            Assert.IsEmpty(referenced, "Rebound expression still references schema types: " + expression);
        }

        private sealed class TypeReferenceCollector : ExpressionVisitor
        {
            private readonly HashSet<Type> _types = new();

            public static IReadOnlyCollection<Type> Collect(Expression expression)
            {
                var collector = new TypeReferenceCollector();
                collector.Visit(expression);
                return collector._types;
            }

            public static bool Mentions(Type type, Type target)
            {
                return type == target
                    || (type.HasElementType && Mentions(type.GetElementType()!, target))
                    || (type.IsGenericType && type.GetGenericArguments().Any(a => Mentions(a, target)));
            }

            public override Expression? Visit(Expression? node)
            {
                if (node is not null)
                {
                    _types.Add(node.Type);
                }

                return base.Visit(node);
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                _types.Add(node.Member.DeclaringType!);
                return base.VisitMember(node);
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                _types.Add(node.Method.DeclaringType!);
                _types.UnionWith(node.Method.GetGenericArguments());
                return base.VisitMethodCall(node);
            }
        }
    }
}
