using LingoHub.Api.DTOs;
using LingoHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LingoHub.Api.Controllers;

// ============================================================
// 文件作用：问答接口的“门口”（完整的 RAG 流程）。
//
//   POST /api/ask
//   请求：{ "question": "What does arvo mean?", "topK": 5 }
//        加上 "useGeneralKnowledge": true → 不用资料，让 AI 用自己的知识回答（需要用户先同意）
//   返回：Gemini 的回答 + 用到的资料块
//
// 这里只检查输入，真正的流程在 RagService 里。
// ============================================================
[ApiController]
[Route("api/ask")]
public class AskController : ControllerBase
{
    // 问题最长多少字符
    private const int MaxQuestionLength = 1000;

    private readonly RagService _rag;

    public AskController(RagService rag) => _rag = rag;

    [HttpPost]
    public async Task<IActionResult> Ask(AskRequest request, CancellationToken ct)
    {
        // ---------- 检查输入 ----------
        var question = request.Question?.Trim();
        if (string.IsNullOrEmpty(question))
            return BadRequest(new { error = "Question is required." });
        if (question.Length > MaxQuestionLength)
            return BadRequest(new { error = $"Question must be at most {MaxQuestionLength} characters." });

        var topK = request.TopK ?? _rag.DefaultTopK;
        if (topK < 1 || topK > _rag.MaxTopK)
            return BadRequest(new { error = $"topK must be between 1 and {_rag.MaxTopK}." });

        // ---------- 问答 ----------
        try
        {
            // 用户已同意 → 用 AI 自己的知识回答；否则走正常的 RAG（只用资料）
            return request.UseGeneralKnowledge == true
                ? Ok(await _rag.AskGeneralAsync(question, ct))
                : Ok(await _rag.AskAsync(question, topK, ct));
        }
        catch (Exception ex) when (ex is EmbeddingException or LlmException)
        {
            // Voyage（检索）或 Gemini（生成）出错 → 502：是“外部的 API”出了问题
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }
}
