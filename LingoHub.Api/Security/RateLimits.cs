namespace LingoHub.Api;

// 限流策略的名字（在 Program.cs 里定义规则，在 Controller 上用 [EnableRateLimiting(...)] 引用）
public static class RateLimits
{
    // 会调用付费 API 的接口：问答（Voyage + Gemini）、检索（Voyage）
    public const string PaidApi = "paid-api";
}
