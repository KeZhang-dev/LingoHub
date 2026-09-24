using System.Diagnostics;
using LingoHub.Api.Data;
using LingoHub.Api.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace LingoHub.Api.Services;

// ============================================================
// 文件作用：检索（Retrieval）= 根据用户的问题，找出“意思最接近”的几块。
//          这是 RAG 里的 R。（以后 AI 回答问题时，就只看这几块）
//
// 怎么找（3 步）：
//   1. 问题 → 向量：用 EmbeddingService，和文档块用“同一个模型”（voyage-4）
//   2. 在 PostgreSQL 里，用 pgvector 计算“问题向量”和“每一块向量”的距离
//   3. 按距离从小到大排序，取前 K 个（距离越小 = 意思越接近）
//
// 用什么距离：余弦距离（cosine distance），pgvector 的运算符是 <=>
//   它比较两个向量的“方向”：
//     距离 0 = 方向完全一样（意思非常接近）
//     距离 1 = 没有关系
//   我们同时返回 similarity = 1 - 距离（越大越相似，更好理解）
//
// 谁调用它：RetrievalController
// 在哪里注册：Program.cs（AddScoped）
// ============================================================
public class RetrievalService
{
    private readonly AppDbContext _db;
    private readonly EmbeddingService _embeddings;
    private readonly RetrievalOptions _options;
    private readonly ILogger<RetrievalService> _logger;

    public RetrievalService(
        AppDbContext db,
        EmbeddingService embeddings,
        IOptions<RetrievalOptions> options,
        ILogger<RetrievalService> logger)
    {
        _db = db;
        _embeddings = embeddings;
        _options = options.Value;
        _logger = logger;
    }

    public int DefaultTopK => _options.DefaultTopK;
    public int MaxTopK => _options.MaxTopK;

    public async Task<RetrievalResultDto> SearchAsync(string query, int topK, CancellationToken ct)
    {
        // ---------- 第 1 步：问题 → 向量 ----------
        var stopwatch = Stopwatch.StartNew();
        var queryVector = new Vector(await _embeddings.EmbedQueryAsync(query, ct));
        var embedMs = stopwatch.ElapsedMilliseconds;

        // ---------- 第 2、3 步：在数据库里算距离、排序、取前 K 个 ----------
        // CosineDistance() 会被 EF Core 翻译成 SQL：
        //   SELECT ... FROM "Chunks"
        //   WHERE "Embedding" IS NOT NULL AND "EmbeddingModel" = 'voyage-4'
        //   ORDER BY "Embedding" <=> @queryVector
        //   LIMIT @topK
        // 距离是在 PostgreSQL 里算的，不需要把 325 个向量都读到 C# 里。
        stopwatch.Restart();
        var model = _embeddings.Model;
        var rows = await _db.Chunks
            .AsNoTracking()
            // 只搜有向量、并且是“同一个模型”生成的块。
            // （不同模型的向量不能比较，就像拿人民币和美元直接比大小）
            .Where(c => c.Embedding != null && c.EmbeddingModel == model)
            .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
            .Take(topK)
            .Select(c => new
            {
                c.Id,
                c.DocumentId,
                c.Document.FileName,
                c.ChunkIndex,
                c.Content,
                Distance = c.Embedding!.CosineDistance(queryVector)
            })
            .ToListAsync(ct);
        var searchMs = stopwatch.ElapsedMilliseconds;

        _logger.LogInformation(
            "Retrieved {Count} chunks for a {QueryLength}-char query (embed {EmbedMs} ms, search {SearchMs} ms, best distance {Best:F4})",
            rows.Count, query.Length, embedMs, searchMs, rows.FirstOrDefault()?.Distance);

        // 整理成返回给前端的格式。Rank = 第几名（1 = 最相似）
        var results = rows
            .Select((r, i) => new RetrievedChunkDto(
                Rank: i + 1,
                ChunkId: r.Id,
                DocumentId: r.DocumentId,
                FileName: r.FileName,
                ChunkIndex: r.ChunkIndex,
                Distance: Math.Round(r.Distance, 4),
                Similarity: Math.Round(1 - r.Distance, 4),
                Content: r.Content))
            .ToList();

        return new RetrievalResultDto(query, model, topK, embedMs, searchMs, results);
    }
}
