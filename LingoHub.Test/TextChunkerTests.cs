using LingoHub.Api.Services;
using Microsoft.Extensions.Options;

namespace LingoHub.Test;

// ============================================================
// 文件作用：TextChunker（切块器）的单元测试。
//
// 什么是单元测试：
//   用代码检查代码。每个 [Fact] / [Theory] 方法就是一个测试。
//   运行 dotnet test 时，xUnit 会自动找到并运行它们，
//   Assert.xxx() 检查结果，结果不对 → 这个测试失败（红色）。
//
// 每个测试都按 3 步写（叫 AAA 模式）：
//   Arrange 准备：造一个切块器和输入文字
//   Act     执行：调用 Split()
//   Assert  检查：结果是不是我们期望的
//
// 为什么测试里用很小的 ChunkSize（比如 50）：
//   用 800 的话要造很长的文字，结果很难用眼睛核对。
//   规则是一样的，数字小一点，更容易算出“应该切成什么样”。
// ============================================================
public class TextChunkerTests
{
    // 造一个切块器。真实程序里设置来自 appsettings.json，
    // 测试里用 Options.Create() 直接传进去。
    private static TextChunker CreateChunker(int chunkSize, int overlap) =>
        new(Options.Create(new ChunkingOptions { ChunkSize = chunkSize, ChunkOverlap = overlap }));

    // 造 n 行文字："Line 01"、"Line 02"……每行正好 7 个字符，方便计算
    private static string Lines(int count) =>
        string.Join('\n', Enumerable.Range(1, count).Select(i => $"Line {i:D2}"));

    // ---------- 基本情况 ----------

    [Fact]
    public void Split_ShortText_ReturnsSingleChunk()
    {
        // 准备：文字比 ChunkSize 短
        var chunker = CreateChunker(chunkSize: 50, overlap: 10);

        // 执行
        var chunks = chunker.Split("arvo 下午 (Noun)");

        // 检查：不用切，原样返回 1 块
        Assert.Single(chunks);
        Assert.Equal("arvo 下午 (Noun)", chunks[0]);
    }

    // [Theory] + [InlineData]：同一个测试，用不同的参数跑多次。
    // 这里检查“最重要的规则”：不管参数是多少，每块都不能超过 ChunkSize。
    [Theory]
    [InlineData(50, 0)]
    [InlineData(50, 20)]
    [InlineData(100, 30)]
    [InlineData(800, 150)]   // 项目真实使用的参数
    public void Split_LongText_NoChunkIsLongerThanChunkSize(int chunkSize, int overlap)
    {
        var chunker = CreateChunker(chunkSize, overlap);
        var text = Lines(500);

        var chunks = chunker.Split(text);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= chunkSize, $"Chunk is {c.Length} chars: {c}"));
    }

    [Fact]
    public void Split_LongText_EveryLineAppearsInSomeChunk()
    {
        // 切块不能“丢内容”：原文的每一行都必须出现在某一块里
        var chunker = CreateChunker(chunkSize: 50, overlap: 10);
        var text = Lines(100);

        var chunks = chunker.Split(text);

        foreach (var line in text.Split('\n'))
            Assert.Contains(chunks, c => c.Split('\n').Contains(line));
    }

    [Fact]
    public void Split_ChunksAreTrimmedAndNotEmpty()
    {
        var chunker = CreateChunker(chunkSize: 50, overlap: 10);

        var chunks = chunker.Split(Lines(100));

        Assert.All(chunks, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c));
            Assert.Equal(c.Trim(), c);   // 开头结尾没有多余的空格/换行
        });
    }

    // ---------- 在“自然位置”切开 ----------

    [Fact]
    public void Split_PrefersParagraphBoundaries()
    {
        // 两个段落各 30 字符，合起来 62 > 50 → 应该正好在空行处切开
        var chunker = CreateChunker(chunkSize: 50, overlap: 10);
        var paragraphA = new string('A', 30);
        var paragraphB = new string('B', 30);

        var chunks = chunker.Split(paragraphA + "\n\n" + paragraphB);

        // 重叠最多 10 字符，但一个完整段落有 30 字符，带不过去 → 不重叠
        Assert.Equal([paragraphA, paragraphB], chunks);
    }

    [Fact]
    public void Split_LongLineWithoutNewlines_CutsAtSentenceEnds()
    {
        // 没有换行的一大段 → 应该在句号后面切，不能把句子切成两半
        var chunker = CreateChunker(chunkSize: 50, overlap: 0);
        var text = string.Join(" ", Enumerable.Range(1, 10).Select(i => $"This is sentence number {i}."));

        var chunks = chunker.Split(text);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.EndsWith(".", c));
    }

    [Fact]
    public void Split_LongSentence_DoesNotCutWordsInHalf()
    {
        // 没有换行、也没有句号 → 应该在空格处切，每个单词保持完整
        var chunker = CreateChunker(chunkSize: 50, overlap: 0);
        var words = Enumerable.Range(1, 40).Select(i => $"word{i}").ToList();

        var chunks = chunker.Split(string.Join(' ', words));

        Assert.True(chunks.Count > 1);
        Assert.All(chunks.SelectMany(c => c.Split(' ')), w => Assert.Contains(w, words));
    }

    [Fact]
    public void Split_SingleHugeWord_IsHardCutWithoutLosingCharacters()
    {
        // 最坏情况：120 个字符，中间没有任何空格和标点 → 只能硬切
        var chunker = CreateChunker(chunkSize: 50, overlap: 10);
        var text = new string('x', 120);

        var chunks = chunker.Split(text);

        // 50 + 50 + 20，拼回去和原文一模一样
        Assert.Equal([50, 50, 20], chunks.Select(c => c.Length));
        Assert.Equal(text, string.Concat(chunks));
    }

    // ---------- 重叠（Overlap） ----------

    [Fact]
    public void Split_WithOverlap_NextChunkStartsWithEndOfPreviousChunk()
    {
        // 每行 7 字符 + 1 个换行。ChunkSize = 50 → 每块放 6 行（6×7 + 5 = 47）。
        // Overlap = 20 → 能带过去 2 行（"Line 05\nLine 06" = 15 字符），3 行就 23 > 20 了。
        var chunker = CreateChunker(chunkSize: 50, overlap: 20);

        var chunks = chunker.Split(Lines(20));

        Assert.Equal("Line 01\nLine 02\nLine 03\nLine 04\nLine 05\nLine 06", chunks[0]);
        Assert.StartsWith("Line 05\nLine 06\nLine 07", chunks[1]);   // 第 5、6 行重复出现
    }

    [Fact]
    public void Split_WithoutOverlap_NextChunkStartsRightAfterPreviousChunk()
    {
        // 对照组：Overlap = 0 → 第 2 块从第 7 行开始，没有重复
        var chunker = CreateChunker(chunkSize: 50, overlap: 0);

        var chunks = chunker.Split(Lines(20));

        Assert.EndsWith("Line 06", chunks[0]);
        Assert.StartsWith("Line 07", chunks[1]);
    }

    // ---------- 接近真实数据 ----------

    [Fact]
    public void Split_VocabularyEntries_KeepsEachEntryWhole()
    {
        // 模拟词汇书：每个条目 = 单词行 + 例句行，条目之间是空行。
        // 每个条目都比 ChunkSize 短，所以不应该有条目被切成两半。
        var chunker = CreateChunker(chunkSize: 120, overlap: 30);
        var entries = Enumerable.Range(1, 20)
            .Select(i => $"word{i} 单词{i} (Noun)\neg: This is example sentence {i}.")
            .ToList();

        var chunks = chunker.Split(string.Join("\n\n", entries));

        // 每个条目都能在某一块里“完整地”找到
        Assert.All(entries, entry => Assert.Contains(chunks, c => c.Contains(entry)));
    }
}
