namespace DocFlowCloud.Application.Jobs;

// 结果文件下载 DTO：
// API 层用它把任务结果转换成 File 响应。
public sealed class JobResultFileDto
{
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = [];
}
