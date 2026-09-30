using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// The unary <c>not</c> operator negates only the operand that follows it, up to the next <c>and</c> or <c>or</c>
    /// outside parentheses, and the negated operand combines with the rest of the filter like any other operand.
    /// </summary>
    [TestFixture]
    public class NotOperatorTests
    {
        private ODataExpressionConverter? _converter;
        private Record[]? _records;

        [SetUp]
        public void Setup()
        {
            _converter = new ODataExpressionConverter();
            _records = new[]
            {
                new Record { Id = 1, Status = "Published", Priority = 1, IsActive = true },
                new Record { Id = 2, Status = "Draft", Priority = 2, IsActive = false },
                new Record { Id = 3, Status = "Archived", Priority = 3, IsActive = true },
                new Record { Id = 4, Status = "Published", Priority = 4, IsActive = false },
            };
        }

        [TestCase("not IsActive", "2,4")]
        [TestCase("not (Status eq 'Draft')", "1,3,4")]
        [TestCase("Status eq 'Published' and not (Status eq 'Draft')", "1,4")]
        [TestCase("Status eq 'Published' and not (Priority eq 1)", "4")]
        [TestCase("Status eq 'Published' and not Priority eq 1", "4")]
        [TestCase("not (Status eq 'Draft') and Priority lt 3", "1")]
        [TestCase("not Status eq 'Draft' and Priority lt 3", "1")]
        [TestCase("Status eq 'Archived' or not IsActive", "2,3,4")]
        [TestCase("Priority eq 1 or not (Priority lt 4)", "1,4")]
        [TestCase("not Status eq 'Published' and not Priority eq 3", "2")]
        [TestCase("not (Status eq 'Published') and not IsActive", "2")]
        [TestCase("not (Status eq 'Published' or IsActive) or Priority eq 4", "2,4")]
        [TestCase("not (Status eq 'Published' and IsActive) and Priority lt 4", "2,3")]
        [TestCase("Priority gt 1 and not (Status eq 'Draft') and not IsActive", "4")]
        [TestCase("Priority gt 1 and not (Status eq 'Draft' or (IsActive and Priority eq 3))", "4")]
        [TestCase("(Priority gt 1 and not (Status eq 'Draft')) or not (Priority gt 1)", "1,3,4")]
        [TestCase("not (not (Status eq 'Draft'))", "2")]
        [TestCase("not not IsActive and Priority gt 1", "3")]
        [TestCase("Priority lt 4 and not startswith(Status, 'Pub')", "2,3")]
        [TestCase("Priority lt 3 and NOT (Status eq 'Draft')", "1")]
        public void NegatesOnlyTheFollowingOperand(string filter, string expectedIds)
        {
            ArgumentNullException.ThrowIfNull(_converter);
            ArgumentNullException.ThrowIfNull(_records);

            var predicate = _converter.Convert<Record>(filter);

            var matches = _records.AsQueryable().Where(predicate).Select(r => r.Id).ToArray();

            CollectionAssert.AreEqual(expectedIds.Split(',').Select(int.Parse).ToArray(), matches, "Failed for " + predicate);
        }

        [TestCase("Priority eq 1 and not (Status eq 'Draft')", "x => ((x.Priority == 1) AndAlso Not((x.Status == \"Draft\")))")]
        [TestCase("not (Status eq 'Draft') and Priority eq 1", "x => (Not((x.Status == \"Draft\")) AndAlso (x.Priority == 1))")]
        [TestCase("Priority eq 1 or not IsActive", "x => ((x.Priority == 1) OrElse Not(x.IsActive))")]
        [TestCase("not IsActive and not (Priority eq 1)", "x => (Not(x.IsActive) AndAlso Not((x.Priority == 1)))")]
        public void BuildsNegationAroundTheFollowingOperandOnly(string filter, string expected)
        {
            ArgumentNullException.ThrowIfNull(_converter);

            var predicate = _converter.Convert<Record>(filter);

            Assert.AreEqual(expected, predicate.ToString());
        }

        [TestCase("Priority eq 1 and not")]
        [TestCase("not and Priority eq 1")]
        [TestCase("Priority eq 1 and not Priority")]
        public void RejectsNotWithoutABooleanOperand(string filter)
        {
            ArgumentNullException.ThrowIfNull(_converter);

            Assert.Catch(() => _converter.Convert<Record>(filter));
        }

        public class Record
        {
            public int Id { get; set; }

            public string Status { get; set; } = string.Empty;

            public int Priority { get; set; }

            public bool IsActive { get; set; }
        }
    }
}
