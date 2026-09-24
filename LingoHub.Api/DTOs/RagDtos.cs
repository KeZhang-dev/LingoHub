namespace LingoHub.Api.DTOs;

// ============================================================
// 文件作用：问答接口（POST /api/ask）的“输入”和“输出”格式。
// ============================================================

// 输入：{ "question": "What does arvo mean?", "topK": 5 }
// TopK 可以不填 → 用默认值（appsettings.json 里的 Retrieval:DefaultTopK）
public record AskRequest(string? Question, int? TopK);

// 输出：
//   Answer         = Gemini 的回答（里面的 [1] [2] 对应 Sources 里的 Rank）
//   LlmModel       = 用哪个 Gemini 模型回答的
//   EmbeddingModel = 用哪个模型做的检索
//   FinishReason   = STOP 表示正常结束
//   *Tokens        = 用了多少 token（成本）
//   *Ms            = 每一步花了多少毫秒（速度）
//   Sources        = 发给 Gemini 的那几块资料（调试用：检查回答有没有依据）
public record RagAnswerDto(
    string Question,
    string Answer,
    string LlmModel,
    string EmbeddingModel,
    string FinishReason,
    int PromptTokens,
    int AnswerTokens,
    int ThinkingTokens,
    long EmbedMs,
    long SearchMs,
    long GenerateMs,
    List<RetrievedChunkDto> Sources);
