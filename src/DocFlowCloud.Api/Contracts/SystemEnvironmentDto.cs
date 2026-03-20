namespace DocFlowCloud.Api.Contracts;

// 环境摘要 DTO：
// 只返回前端联调和环境识别需要的安全摘要，不返回完整 secrets 或连接串。
public sealed class SystemEnvironmentDto
{
    public string ApiEnvironment { get; init; } = string.Empty;

    public string DatabaseServer { get; init; } = string.Empty;

    public string DatabaseName { get; init; } = string.Empty;

    public string RabbitMqHost { get; init; } = string.Empty;

    public string StorageProvider { get; init; } = string.Empty;
}
