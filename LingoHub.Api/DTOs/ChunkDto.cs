namespace LingoHub.Api.DTOs;

// ============================================================
// 文件作用：返回给前端的“一块”的数据形状。
// 用在：GET /api/documents/{id}/chunks（查看切块结果）
//
// Length         = 这块有多少字符，方便你检查切块大小是否合适
// HasEmbedding   = 这块有没有向量
// EmbeddingModel = 向量是哪个模型生成的
// （向量本身有 1024 个数字，太长了，这里不返回）
// ============================================================
public record ChunkDto(
    Guid Id,
    int ChunkIndex,
    int Length,
    bool HasEmbedding,
    string? EmbeddingModel,
    string Content);
