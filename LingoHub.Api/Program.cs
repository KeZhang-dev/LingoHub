using System.Threading.RateLimiting;
using LingoHub.Api;
using LingoHub.Api.Data;
using LingoHub.Api.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

// ============================================================
// 文件作用：后端程序的“入口”。运行 dotnet run 时，从这里开始。
//
// 分两部分：
//   1. builder.Services... → “注册”程序需要的工具（数据库、服务类等）
//      注册以后，ASP.NET 会自动把它们传给需要的类（依赖注入）。
//   2. app... → 设置“请求怎么处理”（CORS、路由到 Controller），然后启动
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// ---------- 第 1 部分：注册工具 ----------

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// 数据库：PostgreSQL + pgvector，连接字符串在 appsettings.json
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.UseVector()));

// 切块设置：从 appsettings.json 的 "Chunking" 读取，并检查数值是否合理。
// ValidateOnStart：数值不对的话，程序启动时就报错（而不是等到上传时才发现）
builder.Services.AddOptions<ChunkingOptions>()
    .Bind(builder.Configuration.GetSection(ChunkingOptions.SectionName))
    .Validate(o => o.ChunkSize > 0 && o.ChunkOverlap >= 0 && o.ChunkOverlap < o.ChunkSize,
        "Chunking: ChunkSize must be > 0, and ChunkOverlap must be >= 0 and smaller than ChunkSize.")
    .ValidateOnStart();

// PDF 处理的三个工具。
// AddSingleton：整个程序只创建一个（它们不保存状态，可以共用）
builder.Services.AddSingleton<PdfTextExtractor>();  // 第 1 步：提取文字
builder.Services.AddSingleton<TextCleaner>();       // 第 2 步：清洗
builder.Services.AddSingleton<TextChunker>();       // 第 3 步：切块

// 第 4 步：向量化。设置从 appsettings.json 的 "Embedding" 读取（密钥从 user-secrets / 环境变量读取）
builder.Services.AddOptions<EmbeddingOptions>()
    .Bind(builder.Configuration.GetSection(EmbeddingOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Model) && Uri.IsWellFormedUriString(o.BaseUrl, UriKind.Absolute)
                   && o.BatchSize is > 0 and <= 1000 && o.TimeoutSeconds > 0,
        "Embedding: Model and BaseUrl are required, BatchSize must be 1-1000, TimeoutSeconds must be > 0.")
    .ValidateOnStart();

// AddHttpClient：给 EmbeddingService 准备一个 HttpClient（用来发网络请求），
// 并设置好 API 地址和超时时间
builder.Services.AddHttpClient<EmbeddingService>((services, http) =>
{
    var options = services.GetRequiredService<IOptions<EmbeddingOptions>>().Value;
    http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");   // 结尾必须有 "/"
    http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

// 检索（搜索）：设置从 appsettings.json 的 "Retrieval" 读取
builder.Services.AddOptions<RetrievalOptions>()
    .Bind(builder.Configuration.GetSection(RetrievalOptions.SectionName))
    .Validate(o => o.MaxTopK > 0 && o.DefaultTopK > 0 && o.DefaultTopK <= o.MaxTopK,
        "Retrieval: DefaultTopK and MaxTopK must be > 0, and DefaultTopK must be <= MaxTopK.")
    .ValidateOnStart();
builder.Services.AddScoped<RetrievalService>();   // 每个请求一个（因为用到数据库）

// 生成：大模型 Gemini。设置从 appsettings.json 的 "Llm" 读取（密钥从 user-secrets / 环境变量读取）
builder.Services.AddOptions<LlmOptions>()
    .Bind(builder.Configuration.GetSection(LlmOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Model) && Uri.IsWellFormedUriString(o.BaseUrl, UriKind.Absolute)
                   && o.MaxOutputTokens > 0 && o.TimeoutSeconds > 0,
        "Llm: Model and BaseUrl are required, MaxOutputTokens and TimeoutSeconds must be > 0.")
    .ValidateOnStart();
builder.Services.AddHttpClient<LlmService>((services, http) =>
{
    var options = services.GetRequiredService<IOptions<LlmOptions>>().Value;
    http.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
    http.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

// RAG 总指挥：检索 + 生成
builder.Services.AddScoped<RagService>();

// 文档服务（总指挥）。
// AddScoped：每个 HTTP 请求创建一个新的（因为它用到的数据库连接也是每个请求一个）
builder.Services.AddScoped<DocumentService>();

// CORS：允许前端网站（localhost:3000）调用这个后端
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:3000"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// 限流：每个 IP 每分钟最多调用几次“要花钱”的接口（问答、检索都会调用付费 API）。
// 防止网站上线后被人刷接口，把 Voyage / Gemini 的额度用光。
// 用法：在 Controller 上加 [EnableRateLimiting(RateLimits.PaidApi)]
var requestsPerMinute = builder.Configuration.GetValue("RateLimiting:RequestsPerMinute", 30);
builder.Services.AddRateLimiter(o =>
{
    o.AddPolicy(RateLimits.PaidApi, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = requestsPerMinute, Window = TimeSpan.FromMinutes(1) }));

    // 超过次数 → 429，并返回前端能直接显示的错误信息
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = (context, ct) => new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
        new { error = "Too many requests. Please wait a minute and try again." }, ct));
});

// 反向代理（部署时前面有 Caddy）：用户的真实 IP 在 X-Forwarded-For 请求头里。
// 只有真的在代理后面才打开，否则别人可以伪造这个请求头来绕过限流。
var behindProxy = builder.Configuration.GetValue<bool>("ReverseProxy:Enabled");
if (behindProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        // 代理在 Docker 内部网络里，地址不固定 → 信任所有代理（后端本身不对外开放端口）
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
    });
}

// ---------- 第 2 部分：设置请求处理，然后启动 ----------

var app = builder.Build();

// 启动时自动建表 / 更新表结构（相当于自动运行 dotnet ef database update）。
// 只在部署时打开（docker-compose.yml 里设置），本地开发还是手动运行命令。
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

if (behindProxy)
    app.UseForwardedHeaders();   // 必须在其它中间件之前，后面拿到的 IP 才是用户的真实 IP

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else if (!behindProxy)
{
    app.UseHttpsRedirection();   // 在代理后面时，HTTPS 由 Caddy 负责
}

app.UseCors();
app.UseRateLimiter();
app.MapControllers();   // 把请求交给 Controllers 文件夹里对应的 Controller

app.Run();
