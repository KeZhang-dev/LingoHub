using LingoHub.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace LingoHub.Test;

// ============================================================
// 文件作用：TextCleaner（清洗器）的单元测试。
//
// Clean() 的输入是“每页的文字”列表，输出是一整篇干净的文字。
// 所以测试里用 new[] { "第 1 页", "第 2 页", ... } 模拟一个 PDF。
//
// NullLogger：TextCleaner 需要一个日志工具，
//   测试里不关心日志，就传一个“什么都不记”的假日志。
// ============================================================
public class TextCleanerTests
{
    private readonly TextCleaner _cleaner = new(NullLogger<TextCleaner>.Instance);

    // ---------- 空格和换行 ----------

    [Fact]
    public void Clean_CollapsesSpacesAndTabs()
    {
        var result = _cleaner.Clean(["  hello    world\t\tfoo  "]);

        Assert.Equal("hello world foo", result);
    }

    [Fact]
    public void Clean_NormalizesWindowsAndOldMacLineEndings()
    {
        // \r\n（Windows）和 \r（老 Mac）→ 统一成 \n
        var result = _cleaner.Clean(["a\r\nb\rc"]);

        Assert.Equal("a\nb\nc", result);
    }

    [Fact]
    public void Clean_CollapsesManyBlankLinesIntoOne()
    {
        // 空行 = 段落分隔，切块器要用它。但连续很多个空行没有意义，只留一个
        var result = _cleaner.Clean(["a\n\n\n\n\nb"]);

        Assert.Equal("a\n\nb", result);
    }

    // ---------- 特殊字符 ----------

    [Fact]
    public void Clean_RemovesNullAndControlCharacters()
    {
        // \0 必须删掉：PostgreSQL 的 text 类型不允许它，存数据库会直接报错
        var result = _cleaner.Clean(["ab\0c\u0007d"]);

        Assert.Equal("abcd", result);
    }

    [Theory]
    [InlineData("ﬁle", "file")]     // ﬁ（一个字符）→ f + i
    [InlineData("oﬀice", "office")] // ﬀ → ff
    public void Clean_ExpandsLigatures(string input, string expected)
    {
        Assert.Equal(expected, _cleaner.Clean([input]));
    }

    [Theory]
    [InlineData("⼲活", "干活")]   // 康熙部首 ⼲ → 干（NFKC 能修）
    [InlineData("⻓期", "长期")]   // 简体部首 ⻓ → 长（NFKC 修不了，靠查表）
    public void Clean_ReplacesRadicalsWithNormalCharacters(string input, string expected)
    {
        // 这两个字“看起来一样”，但编码不同。不替换的话，用户搜“干活”就搜不到
        Assert.Equal(expected, _cleaner.Clean([input]));
    }

    [Fact]
    public void Clean_KeepsChinesePunctuationUnchanged()
    {
        // 只修特殊字符，中文全角标点不能被改成半角
        var result = _cleaner.Clean(["你好，世界？（测试）"]);

        Assert.Equal("你好，世界？（测试）", result);
    }

    // ---------- 页面拼接 ----------

    [Fact]
    public void Clean_JoinsPagesWithBlankLine()
    {
        var result = _cleaner.Clean(["page one", "page two"]);

        Assert.Equal("page one\n\npage two", result);
    }

    [Fact]
    public void Clean_DropsEmptyPages()
    {
        // 空白页不应该留下多余的空行
        var result = _cleaner.Clean(["a", "   ", "b"]);

        Assert.Equal("a\n\nb", result);
    }

    // ---------- 页眉页脚 ----------

    // 造一页：页眉 + 几行正文 + 页脚（页码 "n / 108"）
    private static string Page(int pageNumber, params string[] body) =>
        string.Join('\n', ["Kiwi IT English", .. body, $"{pageNumber} / 108"]);

    [Fact]
    public void Clean_RemovesRepeatedHeadersAndPageNumbers()
    {
        // 4 页，每页顶部都是同一个标题，底部是页码（数字不同，但格式一样）
        var pages = new[]
        {
            Page(1, "arvo 下午", "eg: See you this arvo."),
            Page(2, "brekkie 早餐", "eg: Grab some brekkie."),
            Page(3, "servo 加油站", "eg: Stop at the servo."),
            Page(4, "ute 皮卡", "eg: He drives a ute."),
        };

        var result = _cleaner.Clean(pages);

        // 页眉页脚没了
        Assert.DoesNotContain("Kiwi IT English", result);
        Assert.DoesNotContain("/ 108", result);
        // 正文都还在
        Assert.Contains("arvo 下午", result);
        Assert.Contains("eg: He drives a ute.", result);
    }

    [Fact]
    public void Clean_KeepsRepeatedLinesInTheMiddleOfThePage()
    {
        // 同一行在每页都出现，但在页面“中间”→ 是正文，不能删。
        // （只有每页最上面 2 行、最下面 2 行才会被当作页眉页脚检查）
        const string repeated = "eg: No worries, mate.";
        var pages = Enumerable.Range(1, 4)
            .Select(i => Page(i, $"word{i}a", $"word{i}b", repeated, $"word{i}c", $"word{i}d"))
            .ToArray();

        var result = _cleaner.Clean(pages);

        // 4 页，每页 1 次 → 应该还剩 4 次
        Assert.Equal(4, result.Split('\n').Count(line => line == repeated));
    }

    [Fact]
    public void Clean_WithFewerThanThreePages_DoesNotRemoveHeaders()
    {
        // 页数太少，没法判断“重复”→ 不删任何行
        var result = _cleaner.Clean([Page(1, "arvo 下午"), Page(2, "ute 皮卡")]);

        Assert.Contains("Kiwi IT English", result);
        Assert.Contains("1 / 108", result);
    }
}
