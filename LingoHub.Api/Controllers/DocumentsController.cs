using LingoHub.Api.DTOs;
using LingoHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace LingoHub.Api.Controllers;

[ApiController]
[Route("api/documents")]
public class DocumentsController : ControllerBase
{
    private readonly DocumentService _documents;

    public DocumentsController(DocumentService documents) => _documents = documents;

    [HttpGet]
    public async Task<IEnumerable<DocumentDto>> List(CancellationToken ct) =>
        (await _documents.ListAsync(ct)).Select(DocumentDto.From);

    [HttpPost]
    [RequestSizeLimit(DocumentService.MaxFileSizeBytes + 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, CancellationToken ct)
    {
        var (document, error) = await _documents.SaveAsync(file, ct);
        if (document is null)
            return BadRequest(new { error });

        return Created($"/api/documents/{document.Id}", DocumentDto.From(document));
    }
}
