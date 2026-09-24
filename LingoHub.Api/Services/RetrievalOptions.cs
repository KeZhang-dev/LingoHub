namespace LingoHub.Api.Services;

// ============================================================
// 文件作用：检索（搜索）的“设置”。
// 数值从哪里来：appsettings.json 里的 "Retrieval" 部分。
// 谁使用它：RetrievalService
// ============================================================
public class RetrievalOptions
{
    public const string SectionName = "Retrieval";

    // 用户没说要几块时，默认返回最相似的 5 块
    public int DefaultTopK { get; set; } = 5;

    // 最多返回几块（防止一次要太多）
    public int MaxTopK { get; set; } = 20;
}
