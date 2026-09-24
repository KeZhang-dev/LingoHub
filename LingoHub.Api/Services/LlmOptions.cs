namespace LingoHub.Api.Services;

// ============================================================
// 文件作用：大模型（LLM，这里用 Google Gemini）的“设置”。
//
// 数值从哪里来：appsettings.json 里的 "Llm" 部分。
//
// ⚠️ ApiKey（密钥）不要写在 appsettings.json 里！
//   开发时用 user-secrets 保存（只存在你自己电脑上）：
//     dotnet user-secrets set "Llm:ApiKey" "你的 Gemini key"
//   或者用环境变量：Llm__ApiKey=你的key
//
// 谁使用它：LlmService
// 在哪里注册：Program.cs
// ============================================================
public class LlmOptions
{
    public const string SectionName = "Llm";

    // 用哪个 Gemini 模型。想换模型只改这里（比如 gemini-3.5-flash-lite 更便宜）
    public string Model { get; set; } = "gemini-3.8-flash";

    // Gemini API 的地址
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/";

    // 密钥（从 user-secrets 或环境变量读取）
    public string? ApiKey { get; set; }

    // 回答最多多少 token。
    // 注意：Gemini 3 会先“思考”再回答，思考用的 token 也算在这里面，所以不要设太小。
    public int MaxOutputTokens { get; set; } = 2048;

    // 一次请求最多等多少秒（大模型比 embedding 慢）
    public int TimeoutSeconds { get; set; } = 60;
}
