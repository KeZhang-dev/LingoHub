using LingoHub.Api.Data.Entities;

namespace LingoHub.Api.DTOs;

public record DocumentDto(Guid Id, string FileName, string FileType, DateTime UploadedAt)
{
    public static DocumentDto From(Document d) => new(d.Id, d.FileName, d.FileType, d.UploadedAt);
}
