namespace DocFlowCloud.Application.Jobs;

// DocumentToPdf 任务输出：
// 转换结果同样只保存输出文件的 storage key，底层物理位置由存储实现负责映射。
public sealed class DocumentToPdfJobResult
{
    public string OutputFileName { get; set; } = default!;
    public string OutputStorageKey { get; set; } = default!;
    public DateTime GeneratedAtUtc { get; set; }
}
