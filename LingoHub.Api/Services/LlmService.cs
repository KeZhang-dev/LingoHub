using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace LingoHub.Api.Services;

// ============================================================
// 文件作用：调用 Google Gemini（大模型），让它“写出回答”。（RAG 里的 G = Generation）
//
// 做什么：
//   发送：系统指令（规则） + 用户消息（资料 + 问题）
//   收到：Gemini 写的回答文字
//
// 这个类只负责“和 Gemini 通信”，不知道什么是 RAG、什么是块。
// 提示词（prompt）怎么写，由 RagService 决定。
//
// 谁调用它：RagService.AskAsync()
// 在哪里注册：Program.cs（AddHttpClient）
// ============================================================
public class LlmService
{
    // 最多尝试 3 次（第 1 次 + 失败后重试 2 次）
    private const int MaxAttempts = 3;

    // Gemini 的 JSON 用 camelCase（比如 systemInstruction），Web 默认设置正好是这样。
    // WhenWritingNull：值为 null 的字段不发送
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly LlmOptions _options;
    private readonly ILogger<LlmService> _logger;

    public LlmService(HttpClient http, IOptions<LlmOptions> options, ILogger<LlmService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public string Model => _options.Model;

    // 主方法：规则 + 用户消息 → Gemini 的回答
    public async Task<LlmResult> GenerateAsync(string systemInstruction, string userMessage, CancellationToken ct)
    {
        // 没有配置密钥 → 直接报错，并告诉用户怎么配置
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new LlmException(
                "LLM API key is not configured. Run: dotnet user-secrets set \"Llm:ApiKey\" \"<your Gemini key>\"");

        // 要发送的内容（Gemini generateContent 的格式）：
        // {
        //   "systemInstruction": { "parts": [ { "text": "规则..." } ] },
        //   "contents": [ { "role": "user", "parts": [ { "text": "资料 + 问题" } ] } ],
        //   "generationConfig": { "maxOutputTokens": 2048 }
        // }
        var request = new GeminiRequest(
            SystemInstruction: new GeminiContent(null, [new GeminiPart(systemInstruction)]),
            Contents: [new GeminiContent("user", [new GeminiPart(userMessage)])],
            GenerationConfig: new GeminiGenerationConfig(_options.MaxOutputTokens));

        // 网址：models/gemini-3.8-flash:generateContent
        var url = $"models/{_options.Model}:generateContent";

        for (var attempt = 1; ; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(request, options: Json)
                };
                // 密钥放在请求头里（不放在网址里，因为网址可能会被写进日志）
                message.Headers.Add("x-goog-api-key", _options.ApiKey);
                response = await _http.SendAsync(message, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException ||
                                       (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                // 网络连不上 / 超时 → 可以重试
                if (attempt < MaxAttempts)
                {
                    var wait = RetryDelay(attempt, null);
                    _logger.LogWarning("LLM request failed ({Error}), retrying in {Seconds}s (attempt {Attempt}/{Max})",
                        ex.Message, wait.TotalSeconds, attempt, MaxAttempts);
                    await Task.Delay(wait, ct);
                    continue;
                }
                throw new LlmException("Could not reach the LLM API.", ex);
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                    return await ReadAnswerAsync(response, stopwatch, ct);

                // 429 = 请求太多 / 超出免费额度，5xx = Google 服务器出错 → 等一下再试
                var status = (int)response.StatusCode;
                var canRetry = response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500;
                if (canRetry && attempt < MaxAttempts)
                {
                    var wait = RetryDelay(attempt, response.Headers.RetryAfter?.Delta);
                    _logger.LogWarning("LLM API returned {Status}, retrying in {Seconds}s (attempt {Attempt}/{Max})",
                        status, wait.TotalSeconds, attempt, MaxAttempts);
                    await Task.Delay(wait, ct);
                    continue;
                }

                // 其它错误（400 = 请求格式错误/密钥无效，403 = 没有权限，404 = 模型名错误）→ 不重试
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new LlmException($"LLM API returned {status}: {Truncate(body, 400)}");
            }
        }
    }

    // 读取 Gemini 的回答，处理各种“没有正常回答”的情况
    private async Task<LlmResult> ReadAnswerAsync(HttpResponseMessage response, Stopwatch stopwatch, CancellationToken ct)
    {
        var result = await response.Content.ReadFromJsonAsync<GeminiResponse>(Json, ct)
                     ?? throw new LlmException("LLM API returned an empty response.");

        // 情况 1：问题本身被 Gemini 的安全规则拦截了
        if (result.PromptFeedback?.BlockReason is { } blockReason)
            throw new LlmException($"The LLM blocked the request ({blockReason}).");

        var candidate = result.Candidates?.FirstOrDefault()
                        ?? throw new LlmException("The LLM returned no answer.");

        // 把回答的所有文字片段拼起来。
        // Thought = true 的片段是“思考过程”，不是回答，跳过
        var text = string.Concat(candidate.Content?.Parts?
            .Where(p => p.Thought != true)
            .Select(p => p.Text) ?? []).Trim();

        var finishReason = candidate.FinishReason ?? "UNKNOWN";
        var usage = result.UsageMetadata;

        _logger.LogInformation(
            "LLM {Model} answered in {ElapsedMs} ms: finish {FinishReason}, prompt {PromptTokens} tokens, " +
            "answer {AnswerTokens} tokens, thinking {ThinkingTokens} tokens",
            _options.Model, stopwatch.ElapsedMilliseconds, finishReason,
            usage?.PromptTokenCount, usage?.CandidatesTokenCount, usage?.ThoughtsTokenCount);

        // 情况 2：一个字都没回答（比如因为安全原因停止，或者思考用光了 token）
        if (text.Length == 0)
            throw new LlmException($"The LLM returned no text (finish reason: {finishReason}).");

        // 情况 3：回答被截断了（MAX_TOKENS）→ 仍然返回，但写个警告
        if (finishReason == "MAX_TOKENS")
            _logger.LogWarning("LLM answer was cut off at MaxOutputTokens ({Max}); consider raising Llm:MaxOutputTokens",
                _options.MaxOutputTokens);

        return new LlmResult(text, finishReason,
            usage?.PromptTokenCount ?? 0, usage?.CandidatesTokenCount ?? 0, usage?.ThoughtsTokenCount ?? 0,
            stopwatch.ElapsedMilliseconds);
    }

    private static TimeSpan RetryDelay(int attempt, TimeSpan? retryAfter) =>
        retryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    // ---------- Gemini API 的请求 / 返回格式 ----------
    private record GeminiRequest(
        GeminiContent SystemInstruction, List<GeminiContent> Contents, GeminiGenerationConfig GenerationConfig);
    private record GeminiContent(string? Role, List<GeminiPart> Parts);
    private record GeminiPart(string? Text, bool? Thought = null);
    private record GeminiGenerationConfig(int MaxOutputTokens);

    private record GeminiResponse(
        List<GeminiCandidate>? Candidates, GeminiUsage? UsageMetadata, GeminiPromptFeedback? PromptFeedback);
    private record GeminiCandidate(GeminiContent? Content, string? FinishReason);
    private record GeminiUsage(int? PromptTokenCount, int? CandidatesTokenCount, int? ThoughtsTokenCount);
    private record GeminiPromptFeedback(string? BlockReason);
}

// Gemini 返回的结果
//   Text         = 回答
//   FinishReason = 为什么停止（STOP = 正常结束，MAX_TOKENS = 太长被截断）
//   后面几个     = 用了多少 token、花了多少毫秒（方便你了解成本和速度）
public record LlmResult(
    string Text, string FinishReason, int PromptTokens, int AnswerTokens, int ThinkingTokens, long ElapsedMs);

// 自定义错误：表示“调用大模型失败”
public class LlmException(string message, Exception? inner = null) : Exception(message, inner);
