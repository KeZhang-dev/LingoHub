using System.Text;
using LingoHub.Api.DTOs;

namespace LingoHub.Api.Services;

// ============================================================
// 文件作用：RAG 的“总指挥”——把“检索”和“生成”连起来，回答用户的问题。
//
// RAG = Retrieval（检索） + Augmented（增强） + Generation（生成）
//
// AskAsync() 按顺序做 3 件事：
//   1. 检索（R）：RetrievalService 把问题变成向量，在 pgvector 里找出最相似的 K 块
//   2. 增强（A）：把这 K 块“塞进”提示词（prompt），和问题放在一起
//   3. 生成（G）：LlmService 把提示词发给 Gemini，Gemini 根据这些资料写回答
//
// 为什么不直接问 Gemini？
//   Gemini 没看过你上传的 PDF。直接问它，它只能“凭记忆”回答，可能会编造。
//   把相关的块一起发给它，并要求“只能根据这些资料回答”，回答就有依据了。
//
// 资料里没有答案时：AskAsync() 返回 AnswerSource = "notFound"，前端会问用户
// “要不要让 AI 用自己的知识回答？”。用户同意后才调用 AskGeneralAsync()（不检索，也没有 Sources）。
//
// 谁调用它：AskController
// 在哪里注册：Program.cs（AddScoped）
// ============================================================
public class RagService
{
    // 资料里找不到答案时，要求 Gemini 说的“固定句子”。
    // 固定下来的好处：前端和测试都可以很容易判断“没找到答案”。
    public const string NotEnoughInformation =
        "The provided document does not contain enough information to answer this question.";

    // 系统指令：告诉 Gemini “你是谁、必须遵守什么规则”。
    // 这是防止 AI 编造答案最重要的部分。
    private const string SystemInstruction =
        $"""
        You are LingoHub, an assistant that helps people learn everyday and workplace English used in Australia and New Zealand.

        Answer the user's question using ONLY the numbered context passages in the user message. The passages come from the user's uploaded learning documents.

        Rules:
        - Use only information that is stated in the context. Do not use outside knowledge, and do not invent facts, numbers, definitions or examples.
        - After each statement, cite the passage it comes from, like [1] or [2][3].
        - If the context does not contain the answer, start your reply with exactly this English sentence, word for word, even when the rest of your reply is in another language: "{NotEnoughInformation}" Then you may briefly mention related information that the context does contain, with citations. Never guess the missing answer.
        - Reply in the same language as the question. Keep the answer short and clear. When helpful, quote the example sentence from the context.
        """;

    // 用户同意“用 AI 自己的知识回答”之后用的系统指令。
    // 没有资料可以核对，所以要求它：不确定就直说，不要编造，也不要写 [1] 这种引用。
    private const string GeneralSystemInstruction =
        """
        You are LingoHub, an assistant that helps people learn everyday and workplace English used in Australia and New Zealand.

        The user's own learning documents do not cover this question, and the user has agreed to an answer from your general knowledge.

        Rules:
        - Answer from your general knowledge of English, especially Australian and New Zealand usage.
        - Do not cite passages; do not write [1]-style citations.
        - If you are not sure, or the word or phrase may not exist, say so plainly instead of guessing. Never invent definitions or usage.
        - Reply in the same language as the question. Keep the answer short and clear, and include one natural example sentence when helpful.
        """;

    private readonly RetrievalService _retrieval;
    private readonly LlmService _llm;
    private readonly ILogger<RagService> _logger;

    public RagService(RetrievalService retrieval, LlmService llm, ILogger<RagService> logger)
    {
        _retrieval = retrieval;
        _llm = llm;
        _logger = logger;
    }

    public int DefaultTopK => _retrieval.DefaultTopK;
    public int MaxTopK => _retrieval.MaxTopK;

    public async Task<RagAnswerDto> AskAsync(string question, int topK, CancellationToken ct)
    {
        // ---------- 第 1 步：检索（直接用已经做好的 RetrievalService） ----------
        var retrieval = await _retrieval.SearchAsync(question, topK, ct);

        // 数据库里一块都没有（比如还没上传文档）→ 不用问 Gemini 了
        if (retrieval.Results.Count == 0)
            return new RagAnswerDto(question, NotEnoughInformation, AnswerSources.NotFound, _llm.Model, retrieval.Model,
                "NO_CONTEXT", 0, 0, 0, retrieval.EmbedMs, retrieval.SearchMs, 0, []);

        // ---------- 第 2 步：拼提示词（资料 + 问题） ----------
        var userMessage = BuildUserMessage(question, retrieval.Results);

        // Debug 级别的日志：可以看到发给 Gemini 的完整内容（默认不显示）
        _logger.LogDebug("Prompt sent to {Model}:\n{Prompt}", _llm.Model, userMessage);

        // ---------- 第 3 步：生成（调用 Gemini） ----------
        var answer = await _llm.GenerateAsync(SystemInstruction, userMessage, ct);

        _logger.LogInformation("Answered a {QuestionLength}-char question with {ChunkCount} chunks ({PromptChars} prompt chars)",
            question.Length, retrieval.Results.Count, userMessage.Length);

        // Gemini 用固定句子开头 = 资料里没有答案 → 前端会问用户要不要用 AI 自己的知识回答
        var source = answer.Text.TrimStart().StartsWith(NotEnoughInformation, StringComparison.OrdinalIgnoreCase)
            ? AnswerSources.NotFound
            : AnswerSources.Documents;

        // 返回：回答 + 用到的资料（Sources，方便你检查回答是不是真的来自这些块）
        return new RagAnswerDto(
            question, answer.Text, source, _llm.Model, retrieval.Model, answer.FinishReason,
            answer.PromptTokens, answer.AnswerTokens, answer.ThinkingTokens,
            retrieval.EmbedMs, retrieval.SearchMs, answer.ElapsedMs,
            retrieval.Results);
    }

    // 用户同意后：不检索，直接让 Gemini 用自己的知识回答（没有 Sources）
    public async Task<RagAnswerDto> AskGeneralAsync(string question, CancellationToken ct)
    {
        var answer = await _llm.GenerateAsync(GeneralSystemInstruction, $"<question>{question}</question>", ct);

        _logger.LogInformation("Answered a {QuestionLength}-char question from general knowledge (user approved)",
            question.Length);

        return new RagAnswerDto(
            question, answer.Text, AnswerSources.General, _llm.Model, "", answer.FinishReason,
            answer.PromptTokens, answer.AnswerTokens, answer.ThinkingTokens,
            0, 0, answer.ElapsedMs,
            []);
    }

    // 把检索到的块编号 [1] [2] ...，和问题拼成一条消息。
    // 编号的作用：Gemini 回答时可以写 [1]，你就知道这句话来自哪一块。
    //
    // 拼出来大概是这样：
    //   <context>
    //   <passage id="1" source="Kiwi_...pdf" chunk="1">
    //   arvo 下午 (Noun)
    //   eg: Let's catch up this arvo.
    //   ...
    //   </passage>
    //   ...
    //   </context>
    //
    //   <question>What does arvo mean?</question>
    private static string BuildUserMessage(string question, List<RetrievedChunkDto> chunks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<context>");
        foreach (var chunk in chunks)
        {
            sb.AppendLine($"<passage id=\"{chunk.Rank}\" source=\"{chunk.FileName}\" chunk=\"{chunk.ChunkIndex}\">");
            sb.AppendLine(chunk.Content);
            sb.AppendLine("</passage>");
        }
        sb.AppendLine("</context>");
        sb.AppendLine();
        sb.AppendLine($"<question>{question}</question>");
        return sb.ToString();
    }
}
