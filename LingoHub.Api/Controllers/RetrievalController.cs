using LingoHub.Api.DTOs;
using LingoHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LingoHub.Api.Controllers;

// ============================================================
// 文件作用：检索接口的“门口”。
//
//   POST /api/retrieval
//   请求：{ "query": "How much annual leave do employees get?", "topK": 5 }
//   返回：最相似的 K 块，每块带有内容、第几块、距离和相似度
//
// 这里只检查输入，真正的搜索在 RetrievalService 里。
// 为什么用 POST 不用 GET：问题可能很长、有中文和特殊符号，放在请求体里更方便。
// ============================================================
[ApiController]
[Route("api/retrieval")]
public class RetrievalController : ControllerBase
{
    // 问题最长多少字符（问题应该是一句话，不是一篇文章）
    private const int MaxQueryLength = 1000;

    private readonly RetrievalService _retrieval;

    public RetrievalController(RetrievalService retrieval) => _retrieval = retrieval;

    [HttpPost]
    public async Task<IActionResult> Search(RetrievalRequest request, CancellationToken ct)
    {
        // ---------- 检查输入 ----------
        var query = request.Query?.Trim();
        if (string.IsNullOrEmpty(query))
            return BadRequest(new { error = "Query is required." });
        if (query.Length > MaxQueryLength)
            return BadRequest(new { error = $"Query must be at most {MaxQueryLength} characters." });

        var topK = request.TopK ?? _retrieval.DefaultTopK;   // 没填 → 用默认值
        if (topK < 1 || topK > _retrieval.MaxTopK)
            return BadRequest(new { error = $"topK must be between 1 and {_retrieval.MaxTopK}." });

        // ---------- 搜索 ----------
        try
        {
            return Ok(await _retrieval.SearchAsync(query, topK, ct));
        }
        catch (EmbeddingException ex)
        {
            // 把问题变成向量失败（Voyage API 出错）→ 502
            return StatusCode(StatusCodes.Status502BadGateway, new { error = ex.Message });
        }
    }
}
