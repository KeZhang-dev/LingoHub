using LingoHub.Api.Data;
using LingoHub.Api.Services;
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

// 文档服务（总指挥）。
// AddScoped：每个 HTTP 请求创建一个新的（因为它用到的数据库连接也是每个请求一个）
builder.Services.AddScoped<DocumentService>();

// CORS：允许前端网站（localhost:3000）调用这个后端
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["http://localhost:3000"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

// ---------- 第 2 部分：设置请求处理，然后启动 ----------

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseCors();
app.MapControllers();   // 把请求交给 Controllers 文件夹里对应的 Controller

app.Run();
