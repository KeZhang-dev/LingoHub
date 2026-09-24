using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using LingoHub.Api.Data.Entities;
using Microsoft.Extensions.Options;

namespace LingoHub.Api.Services;

// ============================================================
// 文件作用：把“文字”变成“向量”（Embedding）。（处理流程第 4 步）
//
// 什么是向量：
//   一串数字，比如 [0.012, -0.034, 0.101, ...]，一共 1024 个。
//   意思相近的文字 → 向量也相近。
//   比如 "arvo" 和 "下午" 的向量会很接近，和 "咖啡" 就比较远。
//   以后用户提问时，把问题也变成向量，就能找出“意思最接近”的块。
//
// 怎么变：我们自己算不了，要调用 Voyage AI 的 API（网络请求）：
//   发送：几段文字 + 模型名
//   收到：每段文字对应的一个向量
//
// 输入：一批文字（块的内容）
// 输出：每段文字的向量（float[]，长度 1024）
//
// 谁调用它：DocumentService.EmbedMissingChunksAsync()
// 在哪里注册：Program.cs（AddHttpClient）
// ============================================================
public class EmbeddingService
{
    // 最多尝试 3 次（第 1 次 + 失败后重试 2 次）
    private const int MaxAttempts = 3;

    // Voyage 的 JSON 用 snake_case 命名（比如 input_type），这里设置自动转换：
    // C# 的 InputType ↔ JSON 的 input_type
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _http;
    private readonly EmbeddingOptions _options;
    private readonly ILogger<EmbeddingService> _logger;

    // HttpClient：用来发网络请求。地址和超时已经在 Program.cs 里设置好了
    public EmbeddingService(HttpClient http, IOptions<EmbeddingOptions> options, ILogger<EmbeddingService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    // 当前用的模型名（存进数据库，记录“这个向量是哪个模型生成的”）
    public string Model => _options.Model;

    // 每批多少块
    public int BatchSize => _options.BatchSize;

    // 主方法：一批文字 → 一批向量（顺序和输入一一对应）
    public async Task<List<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        // 没有配置密钥 → 直接报错，并告诉用户怎么配置
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new EmbeddingException(
                "Embedding API key is not configured. Run: dotnet user-secrets set \"Embedding:ApiKey\" \"<your key>\"");

        // 要发送的内容。
        // input_type = "document"：告诉 Voyage 这些是“被搜索的资料”（以后提问时用 "query"），
        //   这样生成的向量更适合搜索。
        // output_dimension：要 1024 维，必须和数据库的 vector(1024) 一样。
        var request = new VoyageRequest(texts, _options.Model, "document", Chunk.EmbeddingDimensions);

        // 发请求。失败的话，某些情况会等一下再重试
        for (var attempt = 1; ; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            HttpResponseMessage response;
            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, "embeddings")
                {
                    Content = JsonContent.Create(request, options: Json)
                };
                // 密钥放在请求头里：Authorization: Bearer <key>
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
                response = await _http.SendAsync(message, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException ||
                                       (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                // 网络连不上 / 超时 → 可以重试
                if (attempt < MaxAttempts)
                {
                    var wait = RetryDelay(attempt, null);
                    _logger.LogWarning("Embedding request failed ({Error}), retrying in {Seconds}s (attempt {Attempt}/{Max})",
                        ex.Message, wait.TotalSeconds, attempt, MaxAttempts);
                    await Task.Delay(wait, ct);
                    continue;
                }
                throw new EmbeddingException("Could not reach the embedding API.", ex);
            }

            using (response)
            {
                // ---- 成功 ----
                if (response.IsSuccessStatusCode)
                    return await ReadVectorsAsync(response, texts.Count, stopwatch, ct);

                // ---- 失败 ----
                // 429 = 请求太多（被限流），5xx = 对方服务器出错 → 等一下再试可能就好了
                var status = (int)response.StatusCode;
                var canRetry = response.StatusCode == HttpStatusCode.TooManyRequests || status >= 500;
                if (canRetry && attempt < MaxAttempts)
                {
                    var wait = RetryDelay(attempt, response.Headers.RetryAfter?.Delta);
                    _logger.LogWarning("Embedding API returned {Status}, retrying in {Seconds}s (attempt {Attempt}/{Max})",
                        status, wait.TotalSeconds, attempt, MaxAttempts);
                    await Task.Delay(wait, ct);
                    continue;
                }

                // 其它错误（比如 401 = 密钥错误，400 = 请求格式错误）→ 重试也没用，直接报错
                var body = await response.Content.ReadAsStringAsync(ct);
                var hint = response.StatusCode == HttpStatusCode.Unauthorized ? " Check that the API key is correct." : "";
                throw new EmbeddingException(
                    $"Embedding API returned {status}: {Truncate(body, 300)}{hint}");
            }
        }
    }

    // 读取返回的 JSON，检查结果是否正确
    private async Task<List<float[]>> ReadVectorsAsync(
        HttpResponseMessage response, int expectedCount, Stopwatch stopwatch, CancellationToken ct)
    {
        var result = await response.Content.ReadFromJsonAsync<VoyageResponse>(Json, ct)
                     ?? throw new EmbeddingException("Embedding API returned an empty response.");

        // 检查 1：发了几段文字，就应该收到几个向量
        if (result.Data.Count != expectedCount)
            throw new EmbeddingException(
                $"Embedding API returned {result.Data.Count} vectors for {expectedCount} texts.");

        // 检查 2：每个向量都必须是 1024 维，否则存不进数据库
        if (result.Data.Any(d => d.Embedding.Length != Chunk.EmbeddingDimensions))
            throw new EmbeddingException(
                $"Embedding API returned vectors that are not {Chunk.EmbeddingDimensions}-dimensional.");

        _logger.LogInformation("Embedded {Count} texts with {Model}: {Tokens} tokens in {ElapsedMs} ms",
            expectedCount, _options.Model, result.Usage?.TotalTokens, stopwatch.ElapsedMilliseconds);

        // 按 index 排序，保证第 i 个向量对应第 i 段文字
        return result.Data.OrderBy(d => d.Index).Select(d => d.Embedding).ToList();
    }

    // 重试前等多久：对方告诉我们等多久（Retry-After）就听它的；
    // 否则第 1 次等 2 秒，第 2 次等 4 秒（越来越久，叫“指数退避”）
    private static TimeSpan RetryDelay(int attempt, TimeSpan? retryAfter) =>
        retryAfter ?? TimeSpan.FromSeconds(Math.Pow(2, attempt));

    private static string Truncate(string text, int max) =>
        text.Length <= max ? text : text[..max] + "…";

    // ---------- Voyage API 的请求 / 返回格式 ----------
    // 发送：{ "input": [...], "model": "voyage-4", "input_type": "document", "output_dimension": 1024 }
    private record VoyageRequest(IReadOnlyList<string> Input, string Model, string InputType, int OutputDimension);

    // 收到：{ "data": [ { "embedding": [...], "index": 0 }, ... ], "usage": { "total_tokens": 123 } }
    private record VoyageResponse(List<VoyageEmbedding> Data, VoyageUsage? Usage);
    private record VoyageEmbedding(float[] Embedding, int Index);
    private record VoyageUsage(int TotalTokens);
}

// 自定义错误：表示“向量生成失败”。
// DocumentService 接住它：文档和块照样保存，只是向量先空着，以后可以补。
public class EmbeddingException(string message, Exception? inner = null)
    : Exception(message, inner);
