namespace LingoHub.Api.DTOs;

// ============================================================
// 文件作用：检索接口（POST /api/retrieval）的“输入”和“输出”格式。
// ============================================================

// 输入（前端发来的）：{ "query": "arvo 是什么意思？", "topK": 5 }
// TopK 可以不填 → 用默认值（appsettings.json 里的 Retrieval:DefaultTopK）
public record RetrievalRequest(string? Query, int? TopK);

// 输出：一共找到了什么
//   EmbedMs  = 把问题变成向量花了多少毫秒（调用 Voyage API）
//   SearchMs = 在数据库里搜索花了多少毫秒
public record RetrievalResultDto(
    string Query,
    string Model,
    int TopK,
    long EmbedMs,
    long SearchMs,
    List<RetrievedChunkDto> Results);

// 输出里的每一块
//   Rank       = 第几名（1 = 最相似）
//   Distance   = 余弦距离，越小越相似（0 = 完全一样）
//   Similarity = 1 - Distance，越大越相似（更直观）
public record RetrievedChunkDto(
    int Rank,
    Guid ChunkId,
    Guid DocumentId,
    string FileName,
    int ChunkIndex,
    double Distance,
    double Similarity,
    string Content);
