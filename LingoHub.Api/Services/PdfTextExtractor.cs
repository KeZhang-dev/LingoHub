using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace LingoHub.Api.Services;

// ============================================================
// 文件作用：从 PDF 文件里“读出文字”。（处理流程第 1 步）
//
// 为什么需要：
//   PDF 不是普通的文本文件，文字藏在复杂的格式里，
//   必须用专门的库（这里用 PdfPig）才能读出来。
//
// 输入：PDF 文件在硬盘上的路径
// 输出：一个列表，每一项 = 一页的文字
//
// 谁调用它：DocumentService.BuildChunks()
// 下一步：把结果交给 TextCleaner（清洗）
// ============================================================
public class PdfTextExtractor
{
    private readonly ILogger<PdfTextExtractor> _logger;

    public PdfTextExtractor(ILogger<PdfTextExtractor> logger) => _logger = logger;

    public List<string> ExtractPages(string filePath, CancellationToken ct)
    {
        try
        {
            // 打开 PDF（using：用完自动关闭文件）
            using var pdf = PdfDocument.Open(filePath);
            var pages = new List<string>(pdf.NumberOfPages);

            // 一页一页地读
            foreach (var page in pdf.GetPages())
            {
                // 如果用户取消了请求，就停下来
                ct.ThrowIfCancellationRequested();

                // ContentOrderTextExtractor：按阅读顺序拿文字，并且保留换行。
                // （保留换行很重要，后面切块时要按“行”来切）
                pages.Add(ContentOrderTextExtractor.GetText(page));
            }

            return pages;
        }
        catch (OperationCanceledException)
        {
            // 取消不是错误，直接往上抛
            throw;
        }
        catch (Exception ex)
        {
            // PDF 坏了、有密码等情况：记录日志，
            // 然后换成一个“用户能看懂”的错误信息
            _logger.LogWarning(ex, "Could not read PDF {FilePath}", filePath);
            throw new PdfProcessingException(
                "The PDF could not be read. It may be damaged or password-protected.", ex);
        }
    }
}

// 自定义错误：表示“这个 PDF 处理不了”。
// DocumentService 会接住它，把 Message 返回给前端显示（HTTP 400）。
public class PdfProcessingException(string message, Exception? inner = null)
    : Exception(message, inner);
