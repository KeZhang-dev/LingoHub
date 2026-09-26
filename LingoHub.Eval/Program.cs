using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LingoHub.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LingoHub.Eval;

// ============================================================
// 文件作用：检索评测（Retrieval Evaluation）。
//
// 回答一个问题：“我的 RAG 找得准不准？换个切块参数会更好还是更差？”
//
// 做法：
//   1. questions.json 里有 20 个问题，每个问题都写好了“正确答案是哪个词条”
//   2. 用后端“真实的”代码处理 PDF：PdfTextExtractor → TextCleaner → TextChunker
//      → EmbeddingService（所以测的就是你的项目本身，不是另写的一套）
//   3. 对每个问题找出最相似的 5 块，看正确词条排在第几
//   4. 换不同的 ChunkSize / Overlap 重复一遍，对比分数
//
// 什么算“找到了”：某一块里同时包含这个词条的 单词行 和 例句
//   （只有单词没有例句 = 词条被切断了，AI 拿到也答不好，算没找到）
//
// 三种用法（在项目根目录运行）：
//   dotnet run --project LingoHub.Eval -- dump [800:150]        导出切块结果，检查词条有没有被切断（不调用 API）
//   dotnet run --project LingoHub.Eval -- compare [800:150 ...] 对比多组切块参数（调用 Voyage API，有缓存）
//   dotnet run --project LingoHub.Eval -- live [网址]            测试正在运行的后端（POST /api/retrieval，走 pgvector）
//
// 结果保存在 LingoHub.Eval/results/，向量缓存在 LingoHub.Eval/cache/（都不提交到 Git）
// ============================================================
internal static class EvalProgram
{
    private const int TopK = 5;

    // 没有指定参数时，compare 对比这几组（ChunkSize:ChunkOverlap）。800:150 是项目现在用的
    private static readonly string[] DefaultConfigs = ["200:0", "400:50", "800:0", "800:150", "1500:200", "3000:300"];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static async Task<int> Main(string[] args)
    {
        var mode = args.FirstOrDefault()?.ToLowerInvariant();
        var rest = args.Skip(1).ToArray();
        var paths = new EvalPaths(FindRepoRoot());

        try
        {
            switch (mode)
            {
                case "dump":
                    return Dump(paths, rest.Length > 0 ? rest : ["800:150"]);
                case "compare":
                    return await CompareAsync(paths, rest.Length > 0 ? rest : DefaultConfigs);
                case "live":
                    return await LiveAsync(paths, rest.FirstOrDefault() ?? "http://localhost:5021");
                default:
                    Console.WriteLine("Usage: dotnet run --project LingoHub.Eval -- dump|compare|live [args]");
                    Console.WriteLine("See the comment at the top of LingoHub.Eval/Program.cs.");
                    return 1;
            }
        }
        catch (Exception ex) when (ex is EvalException or EmbeddingException or HttpRequestException)
        {
            Console.Error.WriteLine($"ERROR: {ex.Message}");
            return 1;
        }
    }

    // ================= dump：导出切块结果 =================

    private static int Dump(EvalPaths paths, string[] configs)
    {
        var data = EvalData.Load(paths, validate: false);
        Directory.CreateDirectory(paths.Results);
        File.WriteAllText(Path.Combine(paths.Results, "clean-text.txt"), data.CleanText);

        foreach (var config in configs.Select(ChunkConfig.Parse))
        {
            var chunks = data.Chunk(config);
            var file = Path.Combine(paths.Results, $"chunks-{config.Size}-{config.Overlap}.txt");
            File.WriteAllText(file, string.Join("\n", chunks.Select((c, i) =>
                $"==================== chunk {i} ({c.Length} chars) ====================\n{c}\n")));

            Console.WriteLine($"{config}: {chunks.Count} chunks, avg {chunks.Average(c => c.Length):F0} chars, " +
                              $"{data.CountBrokenEntries(chunks).Broken} of {data.CountBrokenEntries(chunks).Checked} entries broken → {file}");
        }
        return 0;
    }

    // ================= compare：对比多组切块参数 =================

    private static async Task<int> CompareAsync(EvalPaths paths, string[] configArgs)
    {
        var data = EvalData.Load(paths);
        var configs = configArgs.Select(ChunkConfig.Parse).ToList();
        using var embedder = CachedEmbedder.Create(paths);

        Console.WriteLine($"Embedding model: {embedder.Model}. {data.Questions.Count} questions, top {TopK}.\n");

        // 问题的向量：所有切块参数都一样，只算一次
        var questionVectors = await embedder.EmbedQueriesAsync(data.Questions.Select(q => q.Question).ToList());

        var results = new List<ConfigResult>();
        foreach (var config in configs)
        {
            var chunks = data.Chunk(config);
            Console.WriteLine($"{config}: {chunks.Count} chunks, embedding (cached ones are skipped)...");
            var chunkVectors = await embedder.EmbedDocumentsAsync(chunks);

            // 每个问题：算和每一块的余弦相似度 → 排序 → 取前 5
            var ranks = data.Questions.Select((q, i) =>
            {
                var top = TopChunks(questionVectors[i], chunkVectors, TopK);
                return new QuestionResult(q, FirstMatchRank(data, q, top.Select(t => chunks[t]).ToList()),
                    top.Sum(t => chunks[t].Length));
            }).ToList();

            results.Add(new ConfigResult(config, chunks.Count, chunks.Average(c => c.Length),
                data.CountBrokenEntries(chunks).Broken, ranks));
        }

        var report = BuildCompareReport(data, embedder.Model, results);
        Console.WriteLine();
        Console.WriteLine(report);
        SaveReport(paths, "compare", report);
        return 0;
    }

    // ================= live：测试正在运行的后端 =================

    private static async Task<int> LiveAsync(EvalPaths paths, string baseUrl)
    {
        var data = EvalData.Load(paths);
        using var http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };

        Console.WriteLine($"Testing {baseUrl}/api/retrieval with {data.Questions.Count} questions, top {TopK}...\n");

        var results = new List<QuestionResult>();
        foreach (var q in data.Questions)
        {
            using var response = await http.PostAsJsonAsync("api/retrieval", new { query = q.Question, topK = TopK });
            if (!response.IsSuccessStatusCode)
                throw new EvalException($"POST /api/retrieval returned {(int)response.StatusCode}: " +
                                        await response.Content.ReadAsStringAsync());

            var body = await response.Content.ReadFromJsonAsync<LiveResponse>(Json)
                       ?? throw new EvalException("Empty response from /api/retrieval.");
            var contents = body.Results.Select(r => r.Content).ToList();
            results.Add(new QuestionResult(q, FirstMatchRank(data, q, contents), contents.Sum(c => c.Length)));
        }

        var report = new StringBuilder();
        report.AppendLine($"# Live retrieval eval — {baseUrl} — {DateTime.Now:yyyy-MM-dd HH:mm}");
        report.AppendLine();
        report.AppendLine(MetricsLine(results));
        report.AppendLine();
        AppendQuestionTable(report, results.Select(r => r.Question).ToList(), [("live", results)]);

        Console.WriteLine(report);
        SaveReport(paths, "live", report.ToString());
        return 0;
    }

    // ================= 检索和打分 =================

    // 余弦相似度最高的 k 块的编号（和 pgvector 的 ORDER BY "Embedding" <=> @q LIMIT k 是同一个计算）
    private static List<int> TopChunks(float[] query, IReadOnlyList<float[]> chunks, int k) =>
        chunks.Select((v, i) => (Index: i, Score: Cosine(query, v)))
            .OrderByDescending(x => x.Score)
            .Take(k)
            .Select(x => x.Index)
            .ToList();

    private static double Cosine(float[] a, float[] b)
    {
        double dot = 0, na = 0, nb = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        return dot / (Math.Sqrt(na) * Math.Sqrt(nb));
    }

    // 正确答案出现在第几名（1 = 第一名）。前 5 名里都没有 → null
    private static int? FirstMatchRank(EvalData data, EvalQuestion q, List<string> retrieved)
    {
        for (var i = 0; i < retrieved.Count; i++)
            if (q.Answers.Any(word => data.ContainsEntry(retrieved[i], word)))
                return i + 1;
        return null;
    }

    // ================= 报告 =================

    // Hit@k：正确答案在前 k 名里的问题占多少（只有一个正确答案时，也叫 Recall@k）
    // MRR：平均的 1/名次（第 1 名 = 1，第 2 名 = 0.5，没找到 = 0），越接近 1 越好
    private static string MetricsLine(List<QuestionResult> results)
    {
        double HitAt(int k) => results.Count(r => r.Rank <= k) / (double)results.Count;
        var mrr = results.Average(r => r.Rank is { } rank ? 1.0 / rank : 0);
        return $"Hit@1 {HitAt(1):P0} | Hit@3 {HitAt(3):P0} | Hit@5 {HitAt(5):P0} | MRR {mrr:F2} | " +
               $"avg context {results.Average(r => r.ContextChars):F0} chars";
    }

    private static string BuildCompareReport(EvalData data, string model, List<ConfigResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Retrieval eval — {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine($"Embedding model: {model}. {data.Questions.Count} questions, top {TopK}. " +
                      "A hit = one retrieved chunk contains both the headword line and the example sentence.");
        sb.AppendLine();
        sb.AppendLine("| Size:Overlap | Chunks | Avg chars | Broken entries | Hit@1 | Hit@3 | Hit@5 | MRR | Top-5 context chars |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|---|");
        foreach (var r in results)
        {
            var q = r.Questions;
            double HitAt(int k) => q.Count(x => x.Rank <= k) / (double)q.Count;
            sb.AppendLine($"| {r.Config} | {r.ChunkCount} | {r.AvgChars:F0} | {r.BrokenEntries} | " +
                          $"{HitAt(1):P0} | {HitAt(3):P0} | {HitAt(5):P0} | " +
                          $"{q.Average(x => x.Rank is { } rank ? 1.0 / rank : 0):F2} | " +
                          $"{q.Average(x => x.ContextChars):F0} |");
        }
        sb.AppendLine();
        sb.AppendLine("Per question (rank of the correct entry, - = not in top 5):");
        sb.AppendLine();
        AppendQuestionTable(sb, data.Questions, results.Select(r => (r.Config.ToString(), r.Questions)).ToList());
        return sb.ToString();
    }

    private static void AppendQuestionTable(
        StringBuilder sb, IReadOnlyList<EvalQuestion> questions, List<(string Name, List<QuestionResult> Results)> columns)
    {
        sb.AppendLine($"| # | Type | Question | {string.Join(" | ", columns.Select(c => c.Name))} |");
        sb.AppendLine($"|---|---|---|{string.Concat(columns.Select(_ => "---|"))}");
        for (var i = 0; i < questions.Count; i++)
        {
            var q = questions[i];
            var cells = columns.Select(c => c.Results[i].Rank?.ToString() ?? "-");
            sb.AppendLine($"| {q.Id} | {q.Type} | {q.Question} | {string.Join(" | ", cells)} |");
        }
    }

    private static void SaveReport(EvalPaths paths, string name, string report)
    {
        Directory.CreateDirectory(paths.Results);
        var file = Path.Combine(paths.Results, $"{name}-{DateTime.Now:yyyyMMdd-HHmmss}.md");
        File.WriteAllText(file, report);
        Console.WriteLine($"Saved to {file}");
    }

    // 从当前目录往上找 LingoHub.sln，找到的就是项目根目录
    private static string FindRepoRoot()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "LingoHub.sln")))
                return dir.FullName;
        throw new EvalException("Run this from inside the LingoHub repository (LingoHub.sln not found).");
    }

    private record LiveResponse(List<LiveChunk> Results);
    private record LiveChunk(string Content);
}

// ---------- 数据：PDF、词汇表、问题 ----------

internal record EvalPaths(string Root)
{
    public string Pdf => Path.Combine(Root, "Kiwi_IT_Workplace_English_3000_v2.pdf");
    public string Vocabulary => Path.Combine(Root, "kiwi_it_words_v2.json");
    public string Questions => Path.Combine(Root, "LingoHub.Eval", "questions.json");
    public string Cache => Path.Combine(Root, "LingoHub.Eval", "cache");
    public string Results => Path.Combine(Root, "LingoHub.Eval", "results");
    public string ApiSettings => Path.Combine(Root, "LingoHub.Api", "appsettings.json");
}

internal record VocabEntry(string Word, string Cn, string Eg);

// Answers = 正确的词条（写 kiwi_it_words_v2.json 里的 word，比如 "arvo (Noun)"）。
// 可以写多个：比如问“怎么说谢谢”，cheers 和 chur 都算对
internal record EvalQuestion(int Id, string Type, string Question, List<string> Answers);

internal partial class EvalData
{
    private readonly Dictionary<string, VocabEntry> _byWord;

    private EvalData(string cleanText, List<VocabEntry> vocabulary, List<EvalQuestion> questions)
    {
        CleanText = cleanText;
        Vocabulary = vocabulary;
        Questions = questions;
        _byWord = vocabulary.GroupBy(v => v.Word).ToDictionary(g => g.Key, g => g.First());
    }

    public string CleanText { get; }
    public List<VocabEntry> Vocabulary { get; }
    public List<EvalQuestion> Questions { get; }

    public static EvalData Load(EvalPaths paths, bool validate = true)
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        // 和后端上传时完全一样：提取 → 清洗
        var pages = new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance).ExtractPages(paths.Pdf, CancellationToken.None);
        var cleanText = new TextCleaner(NullLogger<TextCleaner>.Instance).Clean(pages);

        var vocabulary = JsonSerializer.Deserialize<List<VocabEntry>>(File.ReadAllText(paths.Vocabulary), json)!;
        var questions = JsonSerializer.Deserialize<List<EvalQuestion>>(File.ReadAllText(paths.Questions), json)!;

        var data = new EvalData(cleanText, vocabulary, questions);

        // 先检查问题文件本身有没有写错（答案词条不存在，或者 PDF 里根本没有它）。dump 不检查
        if (!validate)
            return data;
        foreach (var q in questions)
        foreach (var word in q.Answers)
        {
            if (!data._byWord.ContainsKey(word))
                throw new EvalException($"Question {q.Id}: answer \"{word}\" is not in kiwi_it_words_v2.json.");
            if (!data.ContainsEntry(cleanText, word))
                throw new EvalException($"Question {q.Id}: answer \"{word}\" was not found in the PDF text.");
        }
        return data;
    }

    // 用后端的 TextChunker 切块
    public List<string> Chunk(ChunkConfig config) =>
        new TextChunker(Options.Create(new ChunkingOptions { ChunkSize = config.Size, ChunkOverlap = config.Overlap }))
            .Split(CleanText);

    // 这段文字里有没有“完整的”词条。PDF 里一个词条长这样：
    //   arvo 下午 (Noun)                  ← 单词行：单词 + 中文
    //   eg: Let's catch up this arvo.     ← 例句
    //   我们今天下午碰个头吧。
    // 单词行和例句都在同一块里，才算完整
    public bool ContainsEntry(string text, string word) => ContainsEntry(Normalize(text), Markers(_byWord[word]));

    private static bool ContainsEntry(string normalizedText, (string Head, string Eg) m) =>
        normalizedText.Contains(m.Head) && normalizedText.Contains(m.Eg);

    // 有多少个词条被切断了（没有任何一块同时包含它的单词行和例句）。
    // 只统计在全文里能找到的词条（JSON 和 PDF 个别格式不一致的，不算）
    public (int Broken, int Checked) CountBrokenEntries(List<string> chunks)
    {
        var normalizedChunks = chunks.Select(Normalize).ToList();
        var fullText = Normalize(CleanText);
        var present = Vocabulary.Select(Markers).Where(m => ContainsEntry(fullText, m)).ToList();
        var broken = present.Count(m => !normalizedChunks.Any(c => ContainsEntry(c, m)));
        return (broken, present.Count);
    }

    // "arvo (Noun)" + "下午" → 单词行 "arvo 下午"；例句 → "eg: let's catch up this arvo."
    private static (string Head, string Eg) Markers(VocabEntry v) =>
        (Normalize(PartOfSpeech().Replace(v.Word, "") + " " + v.Cn), Normalize("eg: " + v.Eg));

    // 比较前统一格式：所有空白（空格、换行）→ 一个空格，全部小写
    private static string Normalize(string text) => Whitespace().Replace(text, " ").Trim().ToLowerInvariant();

    // 结尾的词性，比如 " (Noun)"
    [GeneratedRegex(@"\s*\([^)]*\)\s*$")]
    private static partial Regex PartOfSpeech();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

internal record ChunkConfig(int Size, int Overlap)
{
    // "800:150" → ChunkConfig(800, 150)
    public static ChunkConfig Parse(string text)
    {
        var parts = text.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var size) || !int.TryParse(parts[1], out var overlap)
            || size <= 0 || overlap < 0 || overlap >= size)
            throw new EvalException($"Bad chunk config \"{text}\". Use Size:Overlap, e.g. 800:150 (overlap < size).");
        return new ChunkConfig(size, overlap);
    }

    public override string ToString() => $"{Size}:{Overlap}";
}

internal record QuestionResult(EvalQuestion Question, int? Rank, int ContextChars);

internal record ConfigResult(ChunkConfig Config, int ChunkCount, double AvgChars, int BrokenEntries, List<QuestionResult> Questions);

// ---------- 带缓存的向量化 ----------
// 同一段文字 + 同一个模型 → 向量永远一样，所以存到硬盘上。
// 第二次运行、或者两组参数切出了相同的块，就不用再调用 API（省钱、省时间）。
internal sealed class CachedEmbedder : IDisposable
{
    private readonly HttpClient _http;
    private readonly EmbeddingService _service;
    private readonly string _cacheFile;
    private readonly Dictionary<string, float[]> _cache;

    private CachedEmbedder(HttpClient http, EmbeddingService service, string model, string cacheFile)
    {
        _http = http;
        _service = service;
        Model = model;
        _cacheFile = cacheFile;
        _cache = File.Exists(cacheFile)
            ? JsonSerializer.Deserialize<Dictionary<string, float[]>>(File.ReadAllText(cacheFile))!
            : [];
    }

    public string Model { get; }

    public static CachedEmbedder Create(EvalPaths paths)
    {
        // 和后端读同样的设置：appsettings.json（模型、网址）+ user-secrets（密钥）
        var config = new ConfigurationBuilder()
            .AddJsonFile(paths.ApiSettings)
            .AddUserSecrets(typeof(CachedEmbedder).Assembly)
            .AddEnvironmentVariables()
            .Build();
        var options = config.GetSection(EmbeddingOptions.SectionName).Get<EmbeddingOptions>()
                      ?? throw new EvalException("No \"Embedding\" section in LingoHub.Api/appsettings.json.");

        var http = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
        };
        var logger = LoggerFactory.Create(b => b.AddSimpleConsole().SetMinimumLevel(LogLevel.Warning))
            .CreateLogger<EmbeddingService>();
        var service = new EmbeddingService(http, Options.Create(options), logger);

        Directory.CreateDirectory(paths.Cache);
        return new CachedEmbedder(http, service, options.Model, Path.Combine(paths.Cache, $"{options.Model}.json"));
    }

    public Task<List<float[]>> EmbedDocumentsAsync(IReadOnlyList<string> texts) =>
        EmbedAsync(texts, "document", batch => _service.EmbedDocumentsAsync(batch, CancellationToken.None));

    // 问题一个一个发（和后端检索时一样用 input_type = "query"）
    public Task<List<float[]>> EmbedQueriesAsync(IReadOnlyList<string> texts) =>
        EmbedAsync(texts, "query", async batch =>
        {
            var vectors = new List<float[]>();
            foreach (var text in batch)
                vectors.Add(await _service.EmbedQueryAsync(text, CancellationToken.None));
            return vectors;
        });

    private async Task<List<float[]>> EmbedAsync(
        IReadOnlyList<string> texts, string inputType, Func<List<string>, Task<List<float[]>>> embed)
    {
        var missing = texts.Distinct().Where(t => !_cache.ContainsKey(Key(inputType, t))).ToList();
        var stopwatch = Stopwatch.StartNew();

        foreach (var batch in missing.Chunk(_service.BatchSize))
        {
            var vectors = await WithRateLimitRetryAsync(() => embed(batch.ToList()));
            for (var i = 0; i < batch.Length; i++)
                _cache[Key(inputType, batch[i])] = vectors[i];
            Save();   // 每批都保存：中途失败，下次从这里继续
        }

        if (missing.Count > 0)
            Console.WriteLine($"  embedded {missing.Count} new {inputType} texts in {stopwatch.ElapsedMilliseconds} ms");
        return texts.Select(t => _cache[Key(inputType, t)]).ToList();
    }

    // EmbeddingService 自己会重试 2 次（等 2 秒、4 秒）。
    // 免费账户每分钟请求数很少，还是可能被限流（429）→ 这里再等 1 分钟重试
    private static async Task<T> WithRateLimitRetryAsync<T>(Func<Task<T>> action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (EmbeddingException ex) when (ex.Message.Contains("429") && attempt < 5)
            {
                Console.WriteLine($"  rate limited, waiting 60 s (attempt {attempt}/5)...");
                await Task.Delay(TimeSpan.FromSeconds(60));
            }
        }
    }

    private void Save() => File.WriteAllText(_cacheFile, JsonSerializer.Serialize(_cache));

    private static string Key(string inputType, string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inputType + "\n" + text)));

    public void Dispose() => _http.Dispose();
}

internal class EvalException(string message) : Exception(message);
