namespace LingoHub.Api.Data.Entities;

// ============================================================
// 文件作用：Document = 一个上传的文档（比如一个 PDF）。
// 数据库里对应一张表：Documents。
//
// 注意：PDF 文件本身不存在数据库里，
//       而是存在硬盘的 uploads/ 文件夹，文件名是 {Id}.{FileType}。
//       数据库只存“文档信息”和切好的“文字块”。
//
// 关系：一个 Document → 很多个 Chunk（见 Chunk.cs）
// ============================================================
public class Document
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;   // 原始文件名，比如 "abc.pdf"
    public string FileType { get; set; } = string.Empty;   // 文件类型，目前只有 "pdf"
    public DateTime UploadedAt { get; set; }               // 上传时间（UTC）

    // 导航属性：这个文档的所有块（一对多关系的“多”）
    public List<Chunk> Chunks { get; set; } = [];
}
