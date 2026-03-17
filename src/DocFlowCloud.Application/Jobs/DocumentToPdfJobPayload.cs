namespace DocFlowCloud.Application.Jobs;

// DocumentToPdf 任务输入：
// 数据库里只保存输入文件的 storage key，而不是物理绝对路径。
public sealed class DocumentToPdfJobPayload
{
    public string OriginalFileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string InputStorageKey { get; set; } = default!;
}
