using Microsoft.EntityFrameworkCore;

namespace LinqConvertTools.IntegrationTests
{
    public enum Provider
    {
        PostgreSql,
        SqlServer,
    }

    public class Document
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Status { get; set; }

        public int Priority { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public Author Author { get; set; } = new();

        public List<Tag> Tags { get; set; } = new();
    }

    public class Author
    {
        public string Name { get; set; } = string.Empty;
    }

    public class Tag
    {
        public int Id { get; set; }

        public string Value { get; set; } = string.Empty;
    }

    public sealed class DocumentContext : DbContext
    {
        public DocumentContext(DbContextOptions<DocumentContext> options)
            : base(options)
        {
        }

        public DbSet<Document> Documents => Set<Document>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Document>().OwnsOne(d => d.Author);
        }
    }
}
