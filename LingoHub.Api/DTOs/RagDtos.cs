namespace LingoHub.Api.DTOs;

// ============================================================
// 文件作用：问答接口（POST /api/ask）的“输入”和“输出”格式。
// ============================================================

// 输入：{ "question": "What does arvo mean?", "topK": 5 }
// TopK 可以不填 → 用默认值（appsettings.json 里的 Retrieval:DefaultTopK）
// UseGeneralKnowledge = true：用户已经同意“不用资料，让 AI 用自己的知识回答”（前端只在资料里找不到答案后才会发）
public record AskRequest(string? Question, int? TopK, bool? UseGeneralKnowledge);

// 回答来自哪里（RagAnswerDto.AnswerSource 的值）
public static class AnswerSources
{
    public const string Documents = "documents"; // 来自学习资料，有 Sources
    public const string NotFound = "notFound";   // 资料里没有答案 → 前端问用户要不要用 AI 自己的知识
    public const string General = "general";     // 用户同意后，AI 用自己的知识回答（没有 Sources）
}

// 输出：
//   Answer         = Gemini 的回答（里面的 [1] [2] 对应 Sources 里的 Rank）
//   AnswerSource   = 回答来自哪里：documents / notFound / general（见上面的 AnswerSources）
//   LlmModel       = 用哪个 Gemini 模型回答的
//   EmbeddingModel = 用哪个模型做的检索
//   FinishReason   = STOP 表示正常结束
//   *Tokens        = 用了多少 token（成本）
//   *Ms            = 每一步花了多少毫秒（速度）
//   Sources        = 发给 Gemini 的那几块资料（调试用：检查回答有没有依据）
public record RagAnswerDto(
    string Question,
    string Answer,
    string AnswerSource,
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
