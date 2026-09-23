using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Testcontainers.MsSql;
using Testcontainers.PostgreSql;

namespace LinqConvertTools.IntegrationTests
{
    /// <summary>
    /// Runs converted filters against real PostgreSQL and SQL Server databases in containers and checks which rows match.
    /// Requires a Docker-compatible runtime.
    /// </summary>
    [TestFixture(Provider.PostgreSql)]
    [TestFixture(Provider.SqlServer)]
    public class FilterExecutionTests
    {
        private static readonly Dictionary<Guid, string> Keys = new()
        {
            [Guid.Parse("0f000000-0000-7000-8000-00000000000a")] = "A",
            [Guid.Parse("deadbeef-0000-7000-8000-00000000000b")] = "B",
            [Guid.Parse("0f000000-0000-7000-8000-00000000000c")] = "C",
            [Guid.Parse("0f000000-0000-7000-8000-00000000000d")] = "D",
        };

        private static readonly object[] Filters =
        {
            new object[] { "title eq 'Annual Review'", false, "B" },
            new object[] { "title ne 'Annual Review'", false, "A,C,D" },
            new object[] { "title eq 'O''Brien Memo'", false, "D" },
            new object[] { "title in ('Annual Review', 'Draft Proposal')", false, "B,C" },
            new object[] { "contains(title, 'view')", false, "B" },
            new object[] { "substringof('view', title)", false, "B" },
            new object[] { "startswith(title, 'Draft')", false, "C" },
            new object[] { "endswith(title, 'Memo')", false, "D" },
            new object[] { "not startswith(title, 'Draft')", false, "A,B,D" },
            new object[] { "indexof(title, 'Review') eq 7", false, "B" },
            new object[] { "substring(title, 7) eq 'Review'", false, "B" },
            new object[] { "length(title) eq 12", false, "D" },
            new object[] { "trim(title) eq 'Annual Review'", false, "B" },
            new object[] { "toupper(title) eq 'ANNUAL REVIEW'", false, "B" },
            new object[] { "tolower(title) eq 'annual review'", false, "B" },
            new object[] { "status eq null", false, "C" },
            new object[] { "status ne null", false, "A,B,D" },
            new object[] { "status eq 'Open'", false, "A,D" },
            new object[] { "status ne 'Open'", false, "B,C" },
            new object[] { "priority gt 2", false, "C,D" },
            new object[] { "priority le 2 and status eq 'Open'", false, "A" },
            new object[] { "priority eq 2 or priority eq 5", false, "B,D" },
            new object[] { "priority mod 2 eq 1", false, "A,C,D" },
            new object[] { "priority add 1 eq 3", false, "B" },
            new object[] { "createdAt ge 2025-01-01T00:00:00Z", false, "C,D" },
            new object[] { "createdAt lt datetimeoffset'2024-03-01T00:00:00Z'", false, "A" },
            new object[] { "year(createdAt) eq 2024", false, "A,B" },
            new object[] { "month(createdAt) eq 6", false, "B" },
            new object[] { "day(createdAt) eq 30", false, "D" },
            new object[] { "hour(createdAt) eq 14", false, "B" },
            new object[] { "minute(createdAt) eq 30", false, "A" },
            new object[] { "second(createdAt) eq 59", false, "C" },
            new object[] { "year(createdAt) eq 2025 and month(createdAt) ge 6", false, "D" },
            new object[] { "closedAt eq null", false, "A,C" },
            new object[] { "year(closedAt) eq 2024", false, "B" },
            new object[] { "year(closedAt) ne 2024", false, "A,C,D" },
            new object[] { "month(closedAt) gt 7", false, "D" },
            new object[] { "id eq deadbeef-0000-7000-8000-00000000000b", false, "B" },
            new object[] { "id eq guid'deadbeef-0000-7000-8000-00000000000b'", false, "B" },
            new object[] { "author/name eq 'Ada'", false, "A,C" },
            new object[] { "tags/any(t: t/value eq 'email')", false, "A,B" },
            new object[] { "tags/any()", false, "A,B,D" },
            new object[] { "not tags/any()", false, "C" },
            new object[] { "tags/all(t: t/value eq 'email')", false, "B,C" },
            new object[] { "title eq 'annual review'", true, "B" },
            new object[] { "title ne 'annual review'", true, "A,C,D" },
            new object[] { "title in ('annual review', 'DRAFT PROPOSAL')", true, "B,C" },
            new object[] { "contains(title, 'VIEW')", true, "B" },
            new object[] { "substringof('VIEW', title)", true, "B" },
            new object[] { "startswith(title, 'draft')", true, "C" },
            new object[] { "endswith(title, 'MEMO')", true, "D" },
            new object[] { "indexof(title, 'REVIEW') eq 7", true, "B" },
            new object[] { "status eq 'open'", true, "A,D" },
            new object[] { "status ne 'open'", true, "B,C" },
            new object[] { "author/name eq 'ada'", true, "A,C" },
            new object[] { "tags/any(t: t/value eq 'EMAIL')", true, "A,B" },
        };

        private readonly Provider _provider;
        private IDatabaseContainer? _container;
        private DbContextOptions<DocumentContext>? _options;

        public FilterExecutionTests(Provider provider)
        {
            _provider = provider;
        }

        [OneTimeSetUp]
        public async Task StartDatabase()
        {
            var optionsBuilder = new DbContextOptionsBuilder<DocumentContext>();
            if (_provider == Provider.SqlServer)
            {
                var container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
                _container = container;
                await container.StartAsync();
                optionsBuilder.UseSqlServer(container.GetConnectionString());
            }
            else
            {
                var container = new PostgreSqlBuilder("postgres:17-alpine").Build();
                _container = container;
                await container.StartAsync();
                optionsBuilder.UseNpgsql(container.GetConnectionString());
            }

            _options = optionsBuilder.Options;

            await using var context = new DocumentContext(_options);
            await context.Database.EnsureCreatedAsync();
            context.Documents.AddRange(CreateDocuments());
            await context.SaveChangesAsync();
        }

        [OneTimeTearDown]
        public async Task StopDatabase()
        {
            if (_container != null)
            {
                await _container.DisposeAsync();
            }
        }

        [TestCaseSource(nameof(Filters))]
        public async Task FilterReturnsExpectedRows(string filter, bool ignoreCase, string expected)
        {
            await using var context = new DocumentContext(_options!);
            var predicate = new ODataExpressionConverter().Convert<Document>(filter, ignoreCase);

            var ids = await context.Documents.Where(predicate).Select(d => d.Id).ToListAsync();

            Assert.AreEqual(expected, string.Join(",", ids.Select(id => Keys[id]).OrderBy(key => key, StringComparer.Ordinal)), "Failed for " + predicate);
        }

        private static IEnumerable<Document> CreateDocuments()
        {
            Guid Id(string key) => Keys.Single(pair => pair.Value == key).Key;

            yield return new Document
            {
                Id = Id("A"),
                Title = "Quarterly Report",
                Status = "Open",
                Priority = 1,
                CreatedAt = new DateTimeOffset(2024, 1, 15, 8, 30, 15, TimeSpan.Zero),
                Author = new Author { Name = "Ada" },
                Tags = { new Tag { Value = "email" }, new Tag { Value = "finance" } },
            };
            yield return new Document
            {
                Id = Id("B"),
                Title = "Annual Review",
                Status = "Closed",
                Priority = 2,
                CreatedAt = new DateTimeOffset(2024, 6, 1, 14, 45, 0, TimeSpan.Zero),
                ClosedAt = new DateTimeOffset(2024, 7, 1, 9, 0, 0, TimeSpan.Zero),
                Author = new Author { Name = "Grace" },
                Tags = { new Tag { Value = "email" } },
            };
            yield return new Document
            {
                Id = Id("C"),
                Title = "Draft Proposal",
                Status = null,
                Priority = 3,
                CreatedAt = new DateTimeOffset(2025, 2, 10, 23, 59, 59, TimeSpan.Zero),
                Author = new Author { Name = "Ada" },
            };
            yield return new Document
            {
                Id = Id("D"),
                Title = "O'Brien Memo",
                Status = "Open",
                Priority = 5,
                CreatedAt = new DateTimeOffset(2025, 9, 30, 0, 0, 0, TimeSpan.Zero),
                ClosedAt = new DateTimeOffset(2025, 10, 1, 9, 0, 0, TimeSpan.Zero),
                Author = new Author { Name = "Linus" },
                Tags = { new Tag { Value = "legal" } },
            };
        }
    }
}
