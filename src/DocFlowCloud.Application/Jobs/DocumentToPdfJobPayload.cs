namespace DocFlowCloud.Application.Jobs;

// DocumentToPdf 任务输入：
// 当前把原始文件内容以 Base64 形式放进 payloadJson，方便异步传递和本地调试。
public sealed class DocumentToPdfJobPayload
{
    public string OriginalFileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string FileBytesBase64 { get; set; } = default!;
}
