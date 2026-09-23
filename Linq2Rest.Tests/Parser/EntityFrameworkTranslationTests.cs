using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace LinqConvertTools.Tests.Parser
{
    /// <summary>
    /// Generates SQL for converted filters with the SQL Server and PostgreSQL EF Core providers.
    /// <see cref="EntityFrameworkQueryableExtensions.ToQueryString"/> translates the query without opening a connection.
    /// </summary>
    [TestFixture]
    public class EntityFrameworkTranslationTests
    {
        public enum Provider
        {
            SqlServer,
            PostgreSql,
        }

        [TestCase(Provider.SqlServer, "name eq 'draft'")]
        [TestCase(Provider.SqlServer, "name ne 'draft'")]
        [TestCase(Provider.SqlServer, "name in ('draft', 'final')")]
        [TestCase(Provider.SqlServer, "contains(name, 'raf')")]
        [TestCase(Provider.SqlServer, "startswith(name, 'dr')")]
        [TestCase(Provider.SqlServer, "endswith(name, 'ft')")]
        [TestCase(Provider.SqlServer, "indexof(name, 'raf') eq 1")]
        [TestCase(Provider.SqlServer, "not contains(name, 'raf')")]
        [TestCase(Provider.SqlServer, "tags/any(t: t/value eq 'email')")]
        [TestCase(Provider.SqlServer, "tags/all(t: startswith(t/value, 'e'))")]
        [TestCase(Provider.PostgreSql, "name eq 'draft'")]
        [TestCase(Provider.PostgreSql, "name ne 'draft'")]
        [TestCase(Provider.PostgreSql, "name in ('draft', 'final')")]
        [TestCase(Provider.PostgreSql, "contains(name, 'raf')")]
        [TestCase(Provider.PostgreSql, "startswith(name, 'dr')")]
        [TestCase(Provider.PostgreSql, "endswith(name, 'ft')")]
        [TestCase(Provider.PostgreSql, "indexof(name, 'raf') eq 1")]
        [TestCase(Provider.PostgreSql, "not contains(name, 'raf')")]
        [TestCase(Provider.PostgreSql, "tags/any(t: t/value eq 'email')")]
        [TestCase(Provider.PostgreSql, "tags/all(t: startswith(t/value, 'e'))")]
        public void IgnoreCaseTranslatesToUpper(Provider provider, string filter)
        {
            using var context = new DocumentContext(provider);

            string sql = context.Documents.Where(new ODataExpressionConverter().Convert<Document>(filter, ignoreCase: true)).ToQueryString();

            StringAssert.Contains(provider == Provider.SqlServer ? "UPPER(" : "upper(", sql);
        }

        [TestCase(Provider.SqlServer, "toupper(name) eq 'DRAFT'", "UPPER(")]
        [TestCase(Provider.SqlServer, "tolower(name) eq 'draft'", "LOWER(")]
        [TestCase(Provider.PostgreSql, "toupper(name) eq 'DRAFT'", "upper(")]
        [TestCase(Provider.PostgreSql, "tolower(name) eq 'draft'", "lower(")]
        public void CaseFunctionsTranslate(Provider provider, string filter, string expectedSql)
        {
            using var context = new DocumentContext(provider);

            string sql = context.Documents.Where(new ODataExpressionConverter().Convert<Document>(filter)).ToQueryString();

            StringAssert.Contains(expectedSql, sql);
        }

        [TestCase(Provider.SqlServer, "[d].[ExternalId] IN (")]
        [TestCase(Provider.PostgreSql, "d.\"ExternalId\" IN (")]
        public void InOverGuidMemberTranslates(Provider provider, string expectedSql)
        {
            using var context = new DocumentContext(provider);
            var predicate = new ODataExpressionConverter().Convert<Document>(
                "externalId in (0f000000-0000-7000-8000-000000000001, deadbeef-0000-7000-8000-000000000002)");

            string sql = context.Documents.Where(predicate).ToQueryString();

            StringAssert.Contains(expectedSql, sql);
        }

        [TestCase(Provider.SqlServer)]
        [TestCase(Provider.PostgreSql)]
        public void InvariantCaseFoldingCannotBeTranslated(Provider provider)
        {
            using var context = new DocumentContext(provider);
            var predicate = new ODataExpressionConverter(StringCaseFolding.Invariant).Convert<Document>("name eq 'draft'", ignoreCase: true);

            Assert.Throws<InvalidOperationException>(() => context.Documents.Where(predicate).ToQueryString());
        }

        public class Document
        {
            public int Id { get; set; }

            public string? Name { get; set; }

            public Guid ExternalId { get; set; }

            public List<Tag> Tags { get; set; } = new();
        }

        public class Tag
        {
            public int Id { get; set; }

            public string? Value { get; set; }
        }

        private sealed class DocumentContext : DbContext
        {
            private readonly Provider _provider;

            public DocumentContext(Provider provider)
            {
                _provider = provider;
            }

            public DbSet<Document> Documents => Set<Document>();

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                if (_provider == Provider.SqlServer)
                {
                    optionsBuilder.UseSqlServer("Server=localhost;Database=Translation");
                }
                else
                {
                    optionsBuilder.UseNpgsql("Host=localhost;Database=translation");
                }
            }
        }
    }
}
