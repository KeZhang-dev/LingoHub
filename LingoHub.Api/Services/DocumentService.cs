using LingoHub.Api.Data;
using LingoHub.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LingoHub.Api.Services;

/// <summary>Stores uploaded files and their metadata. Text extraction/chunking is a later stage.</summary>
public class DocumentService
{
    public const long MaxFileSizeBytes = 20 * 1024 * 1024;

    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    private readonly AppDbContext _db;
    private readonly string _uploadsPath;

    public DocumentService(AppDbContext db, IConfiguration config, IWebHostEnvironment env)
    {
        _db = db;
        var configured = config["Storage:UploadsPath"] ?? "uploads";
        _uploadsPath = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);
    }

    public Task<List<Document>> ListAsync(CancellationToken ct) =>
        _db.Documents.AsNoTracking().OrderByDescending(d => d.UploadedAt).ToListAsync(ct);

    /// <returns>The saved document, or an error message if the file is not acceptable.</returns>
    public async Task<(Document? Document, string? Error)> SaveAsync(IFormFile file, CancellationToken ct)
    {
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

        Directory.CreateDirectory(_uploadsPath);
        var path = Path.Combine(_uploadsPath, $"{document.Id}.{document.FileType}");
        await using (var target = File.Create(path))
            await stream.CopyToAsync(target, ct);

        try
        {
            _db.Documents.Add(document);
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            File.Delete(path);
            throw;
        }

        return (document, null);
    }
}
