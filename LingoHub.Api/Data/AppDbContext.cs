using LingoHub.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LingoHub.Api.Data;

// ============================================================
// 文件作用：AppDbContext = C# 代码和 PostgreSQL 数据库之间的“桥”。（EF Core）
//
// - 每个 DbSet<T> 对应数据库里的一张表
// - OnModelCreating 里告诉 EF Core：表的规则（长度、必填、表之间的关系）
// - 改了这里或实体类之后，要生成“迁移”(Migration) 来更新数据库：
//     dotnet ef migrations add 名字
//     dotnet ef database update
//
// 在哪里注册：Program.cs（AddDbContext，连接字符串在 appsettings.json）
// ============================================================
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Document> Documents => Set<Document>();   // Documents 表
    public DbSet<Chunk> Chunks => Set<Chunk>();            // Chunks 表

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 开启 pgvector 扩展（让 PostgreSQL 能存“向量”，Chunk.Embedding 要用）
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Document>(e =>
        {
            e.Property(d => d.FileName).HasMaxLength(255).IsRequired();
            e.Property(d => d.FileType).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<Chunk>(e =>
        {
            // Content 是必填的。不设长度 → PostgreSQL 里是 text 类型（长度不限）
            e.Property(c => c.Content).IsRequired();

            // 向量列：pgvector 的 vector(1024) 类型，只能存 1024 个数字。
            // 可以为空（还没生成向量的块）
            e.Property(c => c.Embedding).HasColumnType($"vector({Chunk.EmbeddingDimensions})");
            e.Property(c => c.EmbeddingModel).HasMaxLength(100);

            // 一对多关系：一个 Document 有很多 Chunk，
            // Chunk 通过 DocumentId 找到它的 Document。
            // Cascade：删除 Document 时，它的所有 Chunk 也自动删除。
            e.HasOne(c => c.Document)
                .WithMany(d => d.Chunks)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
