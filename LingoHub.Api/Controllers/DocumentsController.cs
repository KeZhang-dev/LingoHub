using LingoHub.Api.DTOs;
using LingoHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LingoHub.Api.Controllers;

// ============================================================
// 文件作用：Controller = 接收前端 HTTP 请求的“门口”。
//
// 这里只做三件事：收请求 → 交给 DocumentService 处理 → 返回结果。
// 真正的处理逻辑（PDF、清洗、切块）都在 Services 文件夹里，不写在这里。
//
// 接口列表（网址都以 /api/documents 开头）：
//   GET  /api/documents              所有文档（含每个文档的块数）
//   POST /api/documents              上传 PDF → 自动提取、清洗、切块、保存
//   GET  /api/documents/{id}/chunks  查看某个文档切出来的所有块
// ============================================================
[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly DocumentService _documents;

    public DocumentsController(DocumentService documents) => _documents = documents;

    [HttpGet]
    public Task<List<DocumentDto>> List(CancellationToken ct) => _documents.ListAsync(ct);

    [HttpPost]
    [RequestSizeLimit(DocumentService.MaxFileSizeBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        var (document, error) = await _documents.SaveAsync(file, ct);
        if (document is null)
            return BadRequest(new { error });   // 400：文件有问题，返回原因

        return Created($"/api/documents/{document.Id}", DocumentDto.From(document));  // 201：成功
    }

    // {id:guid}：网址里的 id 必须是 Guid 格式
    [HttpGet("{id:guid}/chunks")]
    public async Task<IActionResult> Chunks(Guid id, CancellationToken ct)
    {
        var chunks = await _documents.GetChunksAsync(id, ct);
        return chunks is null ? NotFound() : Ok(chunks);   // 找不到文档 → 404
    }
}
