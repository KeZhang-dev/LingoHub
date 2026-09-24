using Pgvector;

namespace LingoHub.Api.Data.Entities;

// ============================================================
// 文件作用：Chunk = 文档切出来的“一小块文字”。
// 数据库里对应一张表：Chunks。
//
// 为什么需要：
//   一个 PDF 太长了，后面做 RAG 时不能一次全部交给 AI。
//   所以我们把全文切成很多小块，每块单独保存。
//   以后提问时，只找出“最相关的几块”就够了。
//
// 关系：一个 Document（文档）→ 很多个 Chunk（块）。
// 谁创建它：DocumentService.SaveAsync()（上传 PDF 时自动生成）
// ============================================================
public class Chunk
{
    // 主键：这一块自己的 ID
    public Guid Id { get; set; }

    // 外键：这一块属于哪个文档（指向 Documents 表的 Id）
    public Guid DocumentId { get; set; }

    // 导航属性：用它可以直接拿到所属的 Document 对象（EF Core 用）
    public Document Document { get; set; } = null!;

    // 这一块的文字内容
    public string Content { get; set; } = string.Empty;

    // 顺序号：第几块（从 0 开始）。按它排序就能还原原文的顺序
    public int ChunkIndex { get; set; }

    // ---------- 向量（Embedding） ----------

    // 向量的长度（维度）。数据库的列是 vector(1024)，长度必须完全一样。
    // ⚠️ 想改这个数字：要生成新的迁移，并且所有块都要重新生成向量。
    public const int EmbeddingDimensions = 1024;

    // 这块文字的向量（1024 个数字）。Vector 是 pgvector 提供的类型。
    // 可以是 null（还没生成）：比如 API 失败了，以后可以补上。
    public Vector? Embedding { get; set; }

    // 这个向量是用哪个模型生成的（比如 "voyage-4"）。
    // 为什么要记：不同模型的向量不能互相比较，以后搜索时必须用同一个模型。
    public string? EmbeddingModel { get; set; }
}
