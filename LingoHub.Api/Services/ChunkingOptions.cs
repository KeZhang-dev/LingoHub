namespace LingoHub.Api.Services;

// ============================================================
// 文件作用：切块的“设置”（每块多大、重叠多少）。
//
// 数值从哪里来：appsettings.json 里的 "Chunking" 部分。
//   想改大小？改 appsettings.json 就行，不用改代码，重启后生效。
//
// 谁使用它：TextChunker（切块器）
// 在哪里注册：Program.cs
// ============================================================
public class ChunkingOptions
{
    // appsettings.json 里的名字
    public const string SectionName = "Chunking";

    // 每一块最多多少个字符（英文字母、汉字、空格都算 1 个）。
    // 800 字符 ≈ 这本词汇书里的 10 个单词条目。
    public int ChunkSize { get; set; } = 800;

    // 相邻两块之间重叠多少字符。
    // 为什么要重叠：如果一个条目正好被切在两块中间，
    // 重叠能保证它在下一块里还是完整的。
    public int ChunkOverlap { get; set; } = 150;
}
