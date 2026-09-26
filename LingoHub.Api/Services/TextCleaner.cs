using System.Text;
using System.Text.RegularExpressions;

namespace LingoHub.Api.Services;

// ============================================================
// 文件作用：把 PDF 读出来的“脏文字”整理干净。（处理流程第 2 步）
//
// 为什么需要：
//   PDF 读出来的文字常常有问题，例如：
//   - 多余的空格
//   - 每页都有的页码 / 页眉页脚（比如 "1 / 108"）
//   - 长得像普通字、其实是特殊字符的字（比如 "⼲" 不是 "干"）
//   不清理的话，以后搜索会搜不到，块里也有很多垃圾内容。
//
// 原则：只做“轻度清洗”，不改句子内容。
//
// 输入：每一页的文字（来自 PdfTextExtractor）
// 输出：一整篇干净的文字
// 下一步：交给 TextChunker（切块）
// ============================================================
public partial class TextCleaner
{
    // 每页只检查最上面 2 行和最下面 2 行（页眉页脚只会在这里）
    private const int EdgeLineCount = 2;

    // 至少 3 页才检测页眉页脚（页数太少无法判断“重复”）
    private const int MinPagesForHeaderDetection = 3;

    // 简体部首 → 正常汉字 的对照表。
    // 为什么要手动写：NFKC 能修好 "⼲"→"干" 这类，但修不了下面这些（比如 "⻓"→"长"），
    // 所以只能自己查表替换。这个表是根据真实 PDF 里出现的字整理的。
    private static readonly Dictionary<char, char> SimplifiedRadicals = new()
    {
        ['⻅'] = '见',
        ['⻆'] = '角',
        ['⻉'] = '贝',
        ['⻋'] = '车',
        ['⻓'] = '长',
        ['⻔'] = '门',
        ['⻘'] = '青',
        ['⻚'] = '页',
        ['⻛'] = '风',
        ['⻜'] = '飞',
        ['⻝'] = '食',
        ['⻢'] = '马',
        ['⻣'] = '骨',
        ['⻥'] = '鱼',
        ['⻦'] = '鸟',
        ['⻧'] = '卤',
        ['⻨'] = '麦',
        ['⻩'] = '黄',
        ['⻬'] = '齐',
        ['⻮'] = '齿',
        ['⻰'] = '龙',
    };

    private readonly ILogger<TextCleaner> _logger;

    public TextCleaner(ILogger<TextCleaner> logger) => _logger = logger;

    // 主方法：按顺序执行每一步清洗
    public string Clean(IReadOnlyList<string> pages)
    {
        // 第 1 步：每一页 → 修正特殊字符 → 统一换行 → 去掉多余空格 → 变成“行”的列表
        var pageLines = pages.Select(ToCleanLines).ToList();

        // 第 2 步：找出“很多页都出现”的页眉页脚
        var repeated = FindRepeatedHeadersAndFooters(pageLines);
        if (repeated.Count > 0)
            _logger.LogInformation("Removing repeated header/footer lines matching: {Patterns}",
                string.Join(" | ", repeated));

        // 第 3 步：把页眉页脚删掉，再把每页的行拼回文字
        var cleanPages = pageLines
            .Select(lines => string.Join('\n', RemoveHeadersAndFooters(lines, repeated)).Trim())
            .Where(page => page.Length > 0);

        // 第 4 步：页和页之间用一个空行隔开（空行 = 段落分隔）
        var text = string.Join("\n\n", cleanPages);

        // 第 5 步：连续很多个空行 → 只留一个空行
        return ManyBlankLines().Replace(text, "\n\n");
    }

    // ---------- 第 1 步用到的方法 ----------

    private static List<string> ToCleanLines(string page)
    {
        var text = FixSpecialCharacters(page);

        // Windows 换行 "\r\n" 和老式 Mac 换行 "\r" → 统一成 "\n"
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');

        // 每一行：多个空格/Tab → 一个空格，再去掉行首行尾的空格
        return text.Split('\n')
            .Select(line => ExtraSpaces().Replace(line, " ").Trim())
            .ToList();
    }

    private static string FixSpecialCharacters(string text)
    {
        var sb = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            // 控制字符（看不见的字符）直接丢掉，但保留换行和 Tab。
            // 特别是 "\0"：PostgreSQL 的文本里不允许出现它，会直接报错。
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                continue;

            // 连字：PDF 常把 "fi" "ff" 存成一个特殊字符 "ﬁ" "ﬀ"
            bool isLigature = c >= 'ﬀ' && c <= 'ﬆ';

            // 部首字符：有些 PDF 用 "⼲" (康熙部首) 代替正常的 "干"，
            // 看起来一样，但电脑认为是不同的字，搜索会搜不到
            bool isRadical = c >= '⺀' && c <= '⿟';

            if (SimplifiedRadicals.TryGetValue(c, out var normal))
                sb.Append(normal);   // 查表替换（NFKC 不认识的那些）
            else if (isLigature || isRadical)
                // NFKC 规范化：把这些特殊字符换成对应的普通字符。
                // 只对这几类字符做，其它字符（比如中文标点“，？”）保持原样。
                sb.Append(c.ToString().Normalize(NormalizationForm.FormKC));
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    // ---------- 第 2、3 步用到的方法 ----------

    // 思路：如果同一行文字出现在“一半以上页面”的顶部或底部，就认为它是页眉/页脚。
    // 数字换成 "#" 再比较，这样 "1 / 108" 和 "2 / 108" 会被看成同一种行 "# / #"。
    private static HashSet<string> FindRepeatedHeadersAndFooters(List<List<string>> pageLines)
    {
        if (pageLines.Count < MinPagesForHeaderDetection)
            return [];

        // 统计：每种“顶部/底部行”出现在多少页
        var pageCounts = new Dictionary<string, int>();
        foreach (var lines in pageLines)
        {
            var patterns = EdgeLineIndexes(lines).Select(i => ToPattern(lines[i])).Distinct();
            foreach (var pattern in patterns)
                pageCounts[pattern] = pageCounts.GetValueOrDefault(pattern) + 1;
        }

        // 出现在一半以上页面的 → 页眉页脚
        return pageCounts
            .Where(kv => kv.Value >= pageLines.Count / 2.0)
            .Select(kv => kv.Key)
            .ToHashSet();
    }

    // 只删除“顶部/底部”位置上的页眉页脚，正文中间的行不动
    private static IEnumerable<string> RemoveHeadersAndFooters(List<string> lines, HashSet<string> repeated)
    {
        if (repeated.Count == 0)
            return lines;

        var edges = EdgeLineIndexes(lines);
        return lines.Where((line, i) => !(edges.Contains(i) && repeated.Contains(ToPattern(line))));
    }

    // 找出一页里“最上面 2 行 + 最下面 2 行”的位置（跳过空行）
    private static HashSet<int> EdgeLineIndexes(List<string> lines)
    {
        var nonEmpty = Enumerable.Range(0, lines.Count).Where(i => lines[i].Length > 0).ToList();
        return nonEmpty.Take(EdgeLineCount).Concat(nonEmpty.TakeLast(EdgeLineCount)).ToHashSet();
    }

    private static string ToPattern(string line) => Digits().Replace(line, "#");

    // ---------- 正则表达式（用来查找文字的规则） ----------

    // 除了换行以外的空白（空格、Tab、全角空格等），连续 1 个或多个
    [GeneratedRegex(@"[^\S\n]+")]
    private static partial Regex ExtraSpaces();

    // 连续的数字，比如 "108"
    [GeneratedRegex(@"\d+")]
    private static partial Regex Digits();

    // 3 个或更多换行（= 2 个以上空行）
    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ManyBlankLines();
}
