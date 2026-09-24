namespace LingoHub.Api.Services;

// ============================================================
// 文件作用：Embedding（向量化）的“设置”。
//
// 数值从哪里来：appsettings.json 里的 "Embedding" 部分。
//
// ⚠️ ApiKey（密钥）不要写在 appsettings.json 里！（那个文件会上传到 Git）
//   开发时用 user-secrets 保存（只存在你自己电脑上）：
//     dotnet user-secrets set "Embedding:ApiKey" "你的key"
//   或者用环境变量：Embedding__ApiKey=你的key
//
// 谁使用它：EmbeddingService
// 在哪里注册：Program.cs
// ============================================================
public class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    // 用哪个模型。voyage-4 / voyage-4-lite / voyage-4-large 都支持 1024 维，可以直接换。
    // 注意：换模型后，旧的向量和新的向量不能混在一起比较，需要重新生成。
    public string Model { get; set; } = "voyage-4";

    // Voyage API 的地址
    public string BaseUrl { get; set; } = "https://api.voyageai.com/v1/";

    // 密钥（从 user-secrets 或环境变量读取）
    public string? ApiKey { get; set; }

    // 一次 API 请求发送多少个块。
    // Voyage 最多允许 1000 个；128 个块 ≈ 5 万 token，远低于限制。
    // 我们的 PDF 有 325 块 → 分 3 次请求。
    public int BatchSize { get; set; } = 128;

    // 一次请求最多等多少秒
    public int TimeoutSeconds { get; set; } = 60;
}
