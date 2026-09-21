namespace LingoHub.Api.Data.Entities;

/// <summary>An uploaded source document. The file itself is stored on disk as {Id}.{FileType}.</summary>
public class Document
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
