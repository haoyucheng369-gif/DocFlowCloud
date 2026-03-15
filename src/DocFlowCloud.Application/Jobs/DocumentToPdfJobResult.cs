namespace DocFlowCloud.Application.Jobs;

// DocumentToPdf 任务输出：
// 当前先把生成出来的 PDF 以 Base64 放进结果 JSON，方便前端下载。
public sealed class DocumentToPdfJobResult
{
    public string OutputFileName { get; set; } = default!;
    public string PdfBytesBase64 { get; set; } = default!;
    public DateTime GeneratedAtUtc { get; set; }
}
