using LingoHub.Api.Data.Entities;

namespace LingoHub.Api.DTOs;

// ============================================================
// 文件作用：DTO = 返回给前端的数据“形状”。
//
// 为什么不直接返回数据库对象（Document）：
//   数据库对象可能有很多字段/关联（比如所有 Chunk 的全文和向量），
//   DTO 只挑前端需要的字段，数据更小，也更安全。
//
// ChunkCount         = 一共切成了几块
// EmbeddedChunkCount = 其中几块已经有向量了（两个数一样 = 全部完成）
//
// 谁使用它：DocumentsController、DocumentService
// ============================================================
public record DocumentDto(
    Guid Id,
    string FileName,
    string FileType,
    DateTime UploadedAt,
    int ChunkCount,
    int EmbeddedChunkCount)
{
    // 注意：document.Chunks 必须已经加载，否则两个数都会是 0
    public static DocumentDto From(Document d) =>
        new(d.Id, d.FileName, d.FileType, d.UploadedAt,
            d.Chunks.Count,
            d.Chunks.Count(c => c.Embedding is not null));
}
