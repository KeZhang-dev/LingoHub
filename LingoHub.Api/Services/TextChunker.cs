using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace LingoHub.Api.Services;

// ============================================================
// 文件作用：把一整篇文字切成很多“块”（Chunk）。（处理流程第 3 步）
//
// 为什么需要：
//   RAG 的做法是“先找相关的内容，再交给 AI 回答”。
//   如果整本书是一大块，就没法只找相关的部分。
//   所以要切成小块，每块大约 ChunkSize 个字符。
//
// 怎么切（两个阶段）：
//
//   阶段 A：拆成“小片段”（Piece）
//     尽量在“自然的位置”切开，优先级从高到低：
//       段落（空行） → 行 → 句子 → 单词 → 实在不行才硬切字符
//     只有太长的部分才会继续往下一级拆。
//     这样块里的内容是完整的句子/条目，而不是半个单词。
//
//   阶段 B：把片段“装箱”合并成块
//     一个一个片段往当前块里放，放不下了就开新块。
//     开新块时，把上一块结尾的一小部分（ChunkOverlap）也带过来 → 这就是“重叠”。
//
// 输入：干净的全文（来自 TextCleaner）
// 输出：文字块的列表（DocumentService 会把它们存进数据库）
// ============================================================
public partial class TextChunker
{
    private readonly int _chunkSize;
    private readonly int _overlap;

    // IOptions<ChunkingOptions>：ASP.NET 自动从 appsettings.json 读出设置，传进来
    public TextChunker(IOptions<ChunkingOptions> options)
    {
        _chunkSize = options.Value.ChunkSize;
        _overlap = options.Value.ChunkOverlap;
    }

    // 主方法
    public List<string> Split(string text)
    {
        var pieces = new List<Piece>();
        BreakIntoPieces(text, level: 0, separator: "", pieces);  // 阶段 A
        return MergePieces(pieces);                                // 阶段 B
    }

    // 一个小片段。
    // Separator = 它和前一个片段之间原本的分隔符（"\n\n"、"\n"、" " 或 ""），
    // 合并时放回去，原文的格式就能还原。
    private record Piece(string Separator, string Text);

    // ---------- 阶段 A：拆成小片段 ----------

    // level 表示现在按什么来拆：0=段落 1=行 2=句子 3=单词 4=硬切
    private void BreakIntoPieces(string text, int level, string separator, List<Piece> pieces)
    {
        // 已经够小了 → 直接当作一个片段，不用再拆
        if (text.Length <= _chunkSize)
        {
            pieces.Add(new Piece(separator, text));
            return;
        }

        // 太长了 → 按当前级别拆开
        var (parts, joiner) = level switch
        {
            0 => (text.Split("\n\n"), "\n\n"),         // 按段落
            1 => (text.Split('\n'), "\n"),             // 按行
            2 => (SentenceEnd().Split(text), ""),      // 按句子（标点留在句子里，所以分隔符是 ""）
            3 => (text.Split(' '), " "),               // 按单词
            _ => (text.Chunk(_chunkSize).Select(c => new string(c)).ToArray(), "") // 硬切
        };

        // 拆出来的每一部分，如果还太长，就用“下一级”继续拆（递归）
        var isFirst = true;
        foreach (var part in parts)
        {
            if (part.Length == 0)
                continue;
            BreakIntoPieces(part, level + 1, isFirst ? separator : joiner, pieces);
            isFirst = false;
        }
    }

    // ---------- 阶段 B：合并成块 ----------

    private List<string> MergePieces(List<Piece> pieces)
    {
        var chunks = new List<string>();
        var current = new List<Piece>();   // 正在装的这一块

        foreach (var piece in pieces)
        {
            // 放进这个片段会超过 ChunkSize 吗？
            if (current.Count > 0 && Join([.. current, piece]).Length > _chunkSize)
            {
                // 会超过 → 当前块完成，保存
                chunks.Add(Join(current));

                // 开新块：先带上上一块结尾的重叠部分
                current = TakeOverlap(current);

                // 如果“重叠 + 新片段”还是太长，就少带一点重叠
                while (current.Count > 0 && Join([.. current, piece]).Length > _chunkSize)
                    current.RemoveAt(0);
            }

            current.Add(piece);
        }

        // 最后剩下的也是一块
        if (current.Count > 0)
            chunks.Add(Join(current));

        return chunks;
    }

    // 从一块的末尾往前拿片段，总长度不超过 ChunkOverlap
    private List<Piece> TakeOverlap(List<Piece> chunkPieces)
    {
        var tail = new List<Piece>();
        for (var i = chunkPieces.Count - 1; i >= 0; i--)
        {
            tail.Insert(0, chunkPieces[i]);
            if (Join(tail).Length > _overlap)
            {
                tail.RemoveAt(0);   // 超了，把刚加的这个拿掉
                break;
            }
        }
        return tail;
    }

    // 把片段拼成文字（第一个片段前面不加分隔符）
    private static string Join(List<Piece> pieces)
    {
        var sb = new StringBuilder();
        foreach (var piece in pieces)
        {
            if (sb.Length > 0)
                sb.Append(piece.Separator);
            sb.Append(piece.Text);
        }
        return sb.ToString().Trim();
    }

    // 句子结尾：在 . ! ? 。 ！ ？ 后面切开（标点保留在前一句）
    [GeneratedRegex(@"(?<=[.!?。！？])")]
    private static partial Regex SentenceEnd();
}
