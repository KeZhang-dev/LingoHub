namespace LingoHub.Api.DTOs;

// ============================================================
// 文件作用：返回给前端的“一块”的数据形状。
// 用在：GET /api/documents/{id}/chunks（查看切块结果）
// Length = 这块有多少字符，方便你检查切块大小是否合适
// ============================================================
public record ChunkDto(Guid Id, int ChunkIndex, int Length, string Content);
