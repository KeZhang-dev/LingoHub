using System.Diagnostics;
using LingoHub.Api.Data;
using LingoHub.Api.Data.Entities;
using LingoHub.Api.DTOs;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace LingoHub.Api.Services;

// ============================================================
// 文件作用：管理“文档”的核心服务。这里是整个上传流程的“总指挥”。
//
// 上传一个 PDF 时，SaveAsync() 按顺序做这些事：
//
//   1. 检查文件（不能为空、不能超过 20MB、必须是真的 PDF）
//   2. 把 PDF 原文件保存到 uploads/ 文件夹
//   3. BuildChunks()：
//        PdfTextExtractor  读出文字
//        → TextCleaner     清洗文字
//        → TextChunker     切成块
//   4. 把 Document（文档信息）和所有 Chunk（块）一起存进 PostgreSQL
//   5. EmbedMissingChunksAsync()：
//        EmbeddingService  把每块文字变成向量 → 存回数据库
//
// 如果第 3 或第 4 步失败 → 删除第 2 步保存的文件，不留下“半成品”。
// 如果第 5 步失败（比如 API 出错）→ 文档和块照样保留，向量先空着，
//   以后调用 POST /api/documents/{id}/embeddings 补上。
//
// 谁调用它：DocumentsController（接收前端的 HTTP 请求）
// 在哪里注册：Program.cs（AddScoped：每个请求创建一个新的）
// ============================================================
public class DocumentService
{
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;

    // 真正的 PDF 文件，开头一定是 "%PDF-" 这几个字节
    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    private readonly AppDbContext _db;
    private readonly PdfTextExtractor _extractor;
    private readonly TextCleaner _cleaner;
    private readonly TextChunker _chunker;
    private readonly EmbeddingService _embeddings;
    private readonly ILogger<DocumentService> _logger;
    private readonly string _uploadsPath;

    // 构造函数：需要的工具都由 ASP.NET 自动传进来（这叫“依赖注入”）
    public DocumentService(
        AppDbContext db,
        PdfTextExtractor extractor,
        TextCleaner cleaner,
        TextChunker chunker,
        EmbeddingService embeddings,
        ILogger<DocumentService> logger,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        _db = db;
        _extractor = extractor;
        _cleaner = cleaner;
        _chunker = chunker;
        _embeddings = embeddings;
        _logger = logger;

        var configured = config["Storage:UploadsPath"] ?? "uploads";
        _uploadsPath = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);
    }

    // 获取所有文档（最新的在前），顺便数一下每个文档有几块、几块有向量
    public Task<List<DocumentDto>> ListAsync(CancellationToken ct) =>
        _db.Documents
            .AsNoTracking()
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentDto(d.Id, d.FileName, d.FileType, d.UploadedAt,
                d.Chunks.Count,
                d.Chunks.Count(c => c.Embedding != null)))
            .ToListAsync(ct);

    // 获取一个文档的所有块（按顺序）。文档不存在 → 返回 null
    public async Task<List<ChunkDto>?> GetChunksAsync(Guid documentId, CancellationToken ct)
    {
        if (!await _db.Documents.AnyAsync(d => d.Id == documentId, ct))
            return null;

        return await _db.Chunks
            .AsNoTracking()
            .Where(c => c.DocumentId == documentId)
            .OrderBy(c => c.ChunkIndex)
            .Select(c => new ChunkDto(c.Id, c.ChunkIndex, c.Content.Length,
                c.Embedding != null, c.EmbeddingModel, c.Content))
            .ToListAsync(ct);
    }

    // 给一个文档“补”向量：只处理还没有向量的块。
    // 用在 POST /api/documents/{id}/embeddings。文档不存在 → 返回 null
    public async Task<DocumentDto?> GenerateMissingEmbeddingsAsync(Guid documentId, CancellationToken ct)
    {
        if (!await _db.Documents.AnyAsync(d => d.Id == documentId, ct))
            return null;

        await EmbedMissingChunksAsync(documentId, ct);   // 失败会抛 EmbeddingException

        return await _db.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => new DocumentDto(d.Id, d.FileName, d.FileType, d.UploadedAt,
                d.Chunks.Count,
                d.Chunks.Count(c => c.Embedding != null)))
            .SingleAsync(ct);
    }

    /// <returns>The saved document, or an error message if the file is not acceptable.</returns>
    public async Task<(Document? Document, string? Error)> SaveAsync(IFormFile file, CancellationToken ct)
    {
        // ---------- 第 1 步：检查文件 ----------
        if (file.Length == 0)
            return (null, "The file is empty.");
        if (file.Length > MaxFileSizeBytes)
            return (null, "The file is larger than 20 MB.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".pdf", StringComparison.OrdinalIgnoreCase))
            return (null, "Only PDF files are supported.");

        await using var stream = file.OpenReadStream();
        var header = new byte[PdfMagic.Length];
        var read = await stream.ReadAsync(header, ct);
        if (read < PdfMagic.Length || !header.AsSpan().SequenceEqual(PdfMagic))
            return (null, "The file is not a valid PDF.");
        stream.Position = 0;

        var document = new Document
        {
            Id = Guid.NewGuid(),
            FileName = Path.GetFileName(file.FileName),
            FileType = "pdf",
            UploadedAt = DateTime.UtcNow
        };

        // ---------- 第 2 步：保存原始 PDF 到 uploads/ ----------
        Directory.CreateDirectory(_uploadsPath);
        var path = Path.Combine(_uploadsPath, $"{document.Id}.{document.FileType}");
        await using (var target = File.Create(path))
            await stream.CopyToAsync(target, ct);

        try
        {
            // ---------- 第 3 步：提取 → 清洗 → 切块 ----------
            var chunkTexts = BuildChunks(path, document.FileName, ct);

            // 把每段文字变成 Chunk 对象，挂到 document 上
            document.Chunks = chunkTexts
                .Select((content, index) => new Chunk
                {
                    Id = Guid.NewGuid(),
                    Content = content,
                    ChunkIndex = index
                })
                .ToList();

            // ---------- 第 4 步：存进数据库 ----------
            // 只调用一次 SaveChangesAsync，EF Core 会在“同一个事务”里
            // 插入 1 个 Document + 所有 Chunk：要么全部成功，要么全部不存。
            _db.Documents.Add(document);
            await _db.SaveChangesAsync(ct);
        }
        catch (PdfProcessingException ex)
        {
            // PDF 本身有问题（读不了、没有文字）→ 删文件，告诉用户原因
            File.Delete(path);
            _logger.LogWarning("Rejected {FileName}: {Reason}", document.FileName, ex.Message);
            return (null, ex.Message);
        }
        catch
        {
            // 其它意外错误（比如数据库连不上）→ 删文件，错误继续往上抛（返回 500）
            File.Delete(path);
            throw;
        }

        // ---------- 第 5 步：生成向量 ----------
        // 到这里，文档和块已经安全地存进数据库了。
        // 向量生成失败也没关系：上传仍然算成功，只是向量先空着，以后可以补。
        try
        {
            await EmbedMissingChunksAsync(document.Id, ct);
        }
        catch (EmbeddingException ex)
        {
            _logger.LogWarning(ex,
                "Embedding failed for {FileName} ({DocumentId}); chunks were saved without embeddings. " +
                "Retry with POST /api/documents/{DocumentId}/embeddings",
                document.FileName, document.Id, document.Id);
        }

        return (document, null);
    }

    // 向量化的核心：找出还没有向量的块 → 分批调用 API → 存回数据库
    private async Task EmbedMissingChunksAsync(Guid documentId, CancellationToken ct)
    {
        // 只查 Embedding 为空的块 → 已经有向量的块不会重复生成（省钱、省时间）
        var missing = await _db.Chunks
            .Where(c => c.DocumentId == documentId && c.Embedding == null)
            .OrderBy(c => c.ChunkIndex)
            .ToListAsync(ct);

        if (missing.Count == 0)
        {
            _logger.LogInformation("All chunks of {DocumentId} already have embeddings, nothing to do", documentId);
            return;
        }

        _logger.LogInformation("Generating embeddings for {Count} chunks of {DocumentId} with {Model}",
            missing.Count, documentId, _embeddings.Model);

        // 分批：比如 325 块，每批 128 → 128 + 128 + 69，一共 3 次 API 请求
        foreach (var batch in missing.Chunk(_embeddings.BatchSize))
        {
            // 调用 API：一批文字 → 一批向量（顺序一一对应）
            var vectors = await _embeddings.EmbedDocumentsAsync(batch.Select(c => c.Content).ToList(), ct);

            for (var i = 0; i < batch.Length; i++)
            {
                batch[i].Embedding = new Vector(vectors[i]);   // float[] → pgvector 的 Vector
                batch[i].EmbeddingModel = _embeddings.Model;
            }

            // 每批完成就马上保存。
            // 这样如果第 3 批失败了，前 2 批的向量也不会丢，下次只补第 3 批。
            await _db.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Stored embeddings for {Count} chunks of {DocumentId}", missing.Count, documentId);
    }

    // 处理流程的核心：PDF → 文字 → 干净文字 → 块
    private List<string> BuildChunks(string path, string fileName, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();  // 计时，写进日志

        var pages = _extractor.ExtractPages(path, ct);   // 1. 提取
        var cleanText = _cleaner.Clean(pages);           // 2. 清洗

        // 扫描版 PDF（其实是图片）读不出文字。识别图片里的字（OCR）现在不支持。
        if (string.IsNullOrWhiteSpace(cleanText))
            throw new PdfProcessingException(
                "No text was found in this PDF. Scanned (image-only) PDFs aren't supported yet.");

        var chunks = _chunker.Split(cleanText);         // 3. 切块

        // 日志：方便你在终端里看到每一步的效果
        _logger.LogInformation(
            "Processed {FileName}: {Pages} pages, {RawChars} chars extracted, {CleanChars} chars after cleaning, " +
            "{ChunkCount} chunks (avg {AvgChars} chars) in {ElapsedMs} ms",
            fileName,
            pages.Count,
            pages.Sum(p => p.Length),
            cleanText.Length,
            chunks.Count,
            chunks.Count == 0 ? 0 : (int)chunks.Average(c => c.Length),
            stopwatch.ElapsedMilliseconds);

        return chunks;
    }
}
