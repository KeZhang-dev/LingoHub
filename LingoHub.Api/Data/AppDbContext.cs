using LingoHub.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LingoHub.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Enables the pgvector extension. Chunk/embedding entities come in the RAG stage.
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Document>(e =>
        {
            e.Property(d => d.FileName).HasMaxLength(255).IsRequired();
            e.Property(d => d.FileType).HasMaxLength(20).IsRequired();
        });
    }
}
